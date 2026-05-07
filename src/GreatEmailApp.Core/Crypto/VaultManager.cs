// FILE: src/GreatEmailApp.Core/Crypto/VaultManager.cs
// Created: 2026-05-07 | Revised: 2026-05-07 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Orchestrates the password sync vault end-to-end:
//
//   - On first PC: SetupAsync(passphrase)
//        → generate data key
//        → derive KEK (Argon2id) → wrap data key
//        → encrypt every IMAP password currently in Credential Manager
//        → push the vault doc to Firestore
//        → cache the unwrapped data key locally (DPAPI) so future launches
//          don't re-prompt.
//
//   - On a new PC: UnlockAsync(passphrase)
//        → pull vault doc
//        → derive KEK with the doc's salt → unwrap data key
//        → decrypt every password → write each into local Credential Manager
//        → cache data key locally.
//
//   - When adding/updating one account: UpsertPasswordAsync(accountId, password)
//        → encrypt with cached data key (or prompt via callback if absent)
//        → push.
//
//   - LockAndForget(): drop in-memory key + delete vault.dat. Used when the
//        user wants this PC to forget the cached key (no re-prompt avoidance).
//
// All long-running KDF work runs on Task.Run so callers can stay on the UI thread.

using GreatEmailApp.Core.Services;
using GreatEmailApp.Core.Storage;
using GreatEmailApp.Core.Sync;

namespace GreatEmailApp.Core.Crypto;

public enum VaultStatus
{
    /// <summary>No vault doc on Firestore. User needs SetupAsync.</summary>
    NotSetUp,
    /// <summary>Vault doc exists but this PC has no cached data key. User needs UnlockAsync.</summary>
    Locked,
    /// <summary>Data key is cached locally; encrypt/decrypt work without prompting.</summary>
    Unlocked,
}

public sealed class VaultManager
{
    private readonly IVaultSync _sync;
    private readonly ILocalDataKeyCache _cache;
    private readonly ICredentialStore _creds;
    private readonly IAccountStore _accounts;

    private byte[]? _dataKey;        // null when locked
    private byte[]? _kdfSalt;        // cached after pull/setup so we can rewrap on passphrase change later
    private VaultSnapshot? _last;    // most recent pulled snapshot (for additive pushes)

    public VaultManager(IVaultSync sync, ILocalDataKeyCache cache, ICredentialStore creds, IAccountStore accounts)
    {
        _sync = sync;
        _cache = cache;
        _creds = creds;
        _accounts = accounts;

        // Try to restore the data key from local DPAPI cache. If present we're
        // immediately Unlocked — but we still need the salt + remote doc later
        // for rewrap operations, so we lazy-fetch on first push.
        _dataKey = _cache.Read();
    }

    public bool IsUnlocked => _dataKey is not null;

    /// <summary>
    /// Quick state probe. Does a Firestore pull. Caller should run off the UI thread.
    /// </summary>
    public async Task<Result<VaultStatus>> ProbeAsync(CancellationToken ct = default)
    {
        var pull = await _sync.PullAsync(ct).ConfigureAwait(false);
        if (pull is Result<VaultSnapshot?>.Fail f)
            return Result.Fail<VaultStatus>(f.Error);

        var snap = ((Result<VaultSnapshot?>.Ok)pull).Value;
        if (snap is null) return Result.Ok(VaultStatus.NotSetUp);

        _last = snap;
        _kdfSalt = snap.KdfSalt;

        return Result.Ok(_dataKey is null ? VaultStatus.Locked : VaultStatus.Unlocked);
    }

    /// <summary>
    /// First-time setup. Generates a fresh data key, encrypts every password
    /// currently stored in Credential Manager, and pushes the vault doc.
    /// </summary>
    public async Task<Result<bool>> SetupAsync(string passphrase, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(passphrase))
            return Result.Fail<bool>("Passphrase cannot be empty.");

        // KDF + key generation off the UI thread — Argon2id is intentionally slow.
        var (dataKey, salt, wrapped) = await Task.Run(() =>
        {
            var dk = PasswordVault.NewDataKey();
            var s = PasswordVault.NewKdfSalt();
            var kek = PasswordVault.DeriveKek(passphrase, s);
            try { return (dk, s, PasswordVault.WrapDataKey(dk, kek)); }
            finally { Array.Clear(kek); }
        }, ct).ConfigureAwait(false);

        // Encrypt every account's currently-stored password.
        var encrypted = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var account in _accounts.LoadAll())
        {
            var c = _creds.Read(account.Id);
            if (c is null) continue;  // No password locally → nothing to upload for this account.
            encrypted[account.Id] = PasswordVault.EncryptPassword(c.Value.Password, dataKey, account.Id);
        }

        var snapshot = new VaultSnapshot(salt, wrapped, encrypted, DateTimeOffset.UtcNow);
        var push = await _sync.PushAsync(snapshot, ct).ConfigureAwait(false);
        if (push is Result<bool>.Fail pf)
        {
            Array.Clear(dataKey);
            return Result.Fail<bool>($"Vault setup push failed: {pf.Error}");
        }

        _dataKey = dataKey;
        _kdfSalt = salt;
        _last = snapshot;
        _cache.Write(dataKey);
        return Result.Ok(true);
    }

    /// <summary>
    /// On a new PC: pull the vault, derive the KEK, unwrap the data key, and
    /// decrypt every password into the local Credential Manager. Returns the
    /// number of passwords restored. Re-running this is the "resync passwords"
    /// path — safe and idempotent.
    /// </summary>
    public async Task<Result<int>> UnlockAsync(string passphrase, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(passphrase))
            return Result.Fail<int>("Passphrase cannot be empty.");

        var pull = await _sync.PullAsync(ct).ConfigureAwait(false);
        if (pull is Result<VaultSnapshot?>.Fail pf)
            return Result.Fail<int>(pf.Error);
        var snap = ((Result<VaultSnapshot?>.Ok)pull).Value;
        if (snap is null)
            return Result.Fail<int>("No vault on the server. Use Set up password sync on a PC that already has your accounts.");

        var dataKey = await Task.Run(() =>
        {
            var kek = PasswordVault.DeriveKek(passphrase, snap.KdfSalt);
            try { return PasswordVault.UnwrapDataKey(snap.WrappedDataKey, kek); }
            finally { Array.Clear(kek); }
        }, ct).ConfigureAwait(false);

        if (dataKey is null)
            return Result.Fail<int>("That passphrase didn't unlock the vault. Try again.");

        // Decrypt and write into Credential Manager. Skip accounts the user
        // doesn't have locally yet — those will get filled in when accounts.json
        // syncs and ApplyToLocalCredentials is called again, or on next Unlock.
        var localAccountIds = _accounts.LoadAll().Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        int restored = 0;
        foreach (var (accountId, blob) in snap.EncryptedPasswords)
        {
            if (!localAccountIds.Contains(accountId)) continue;
            var pw = PasswordVault.DecryptPassword(blob, dataKey, accountId);
            if (pw is null) continue;  // Tampered or wrong-account blob; skip rather than fail the whole unlock.
            // Username is whatever the local account record has; we don't store it in the vault.
            var username = _accounts.LoadAll().FirstOrDefault(a => a.Id == accountId)?.Username ?? "";
            _creds.Save(accountId, username, pw);
            restored++;
        }

        _dataKey = dataKey;
        _kdfSalt = snap.KdfSalt;
        _last = snap;
        _cache.Write(dataKey);
        return Result.Ok(restored);
    }

    /// <summary>
    /// Push a single account's password into the vault. Uses the cached data
    /// key if available; returns Fail("Locked …") otherwise so the caller can
    /// prompt for the passphrase via UnlockAsync first.
    /// </summary>
    public async Task<Result<bool>> UpsertPasswordAsync(string accountId, string password, CancellationToken ct = default)
    {
        if (_dataKey is null)
            return Result.Fail<bool>("Vault is locked. Unlock with the master passphrase first.");

        // Pull the latest snapshot if we don't have one in memory — we need
        // the existing salt + wrapped key to round-trip the doc.
        if (_last is null || _kdfSalt is null)
        {
            var pull = await _sync.PullAsync(ct).ConfigureAwait(false);
            if (pull is Result<VaultSnapshot?>.Fail pf) return Result.Fail<bool>(pf.Error);
            var s = ((Result<VaultSnapshot?>.Ok)pull).Value;
            if (s is null) return Result.Fail<bool>("Vault has not been set up yet.");
            _last = s;
            _kdfSalt = s.KdfSalt;
        }

        var encrypted = new Dictionary<string, byte[]>(_last.EncryptedPasswords, StringComparer.Ordinal)
        {
            [accountId] = PasswordVault.EncryptPassword(password, _dataKey, accountId),
        };
        var next = new VaultSnapshot(_last.KdfSalt, _last.WrappedDataKey, encrypted, DateTimeOffset.UtcNow);

        var push = await _sync.PushAsync(next, ct).ConfigureAwait(false);
        if (push is Result<bool>.Fail f) return Result.Fail<bool>(f.Error);
        _last = next;
        return Result.Ok(true);
    }

    /// <summary>Drop the in-memory data key and delete the local cache file.</summary>
    public void LockAndForget()
    {
        if (_dataKey is not null) Array.Clear(_dataKey);
        _dataKey = null;
        _cache.Clear();
    }
}
