// FILE: src/GreatEmailApp.Core/Sync/IVaultSync.cs
// Created: 2026-05-07 | Revised: 2026-05-07 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Sync;

/// <summary>
/// Snapshot of the user's password vault as it lives in Firestore. All byte
/// arrays are opaque blobs from <see cref="GreatEmailApp.Core.Crypto.PasswordVault"/>;
/// nothing on the wire (or in this record) is plaintext.
/// </summary>
/// <param name="KdfSalt">Random per-user salt for Argon2id. Required to derive the KEK.</param>
/// <param name="WrappedDataKey">Data key encrypted with the KEK (AES-GCM nonce||ct||tag).</param>
/// <param name="EncryptedPasswords">accountId → AES-GCM blob of the IMAP password.</param>
/// <param name="UpdatedAt">Server-side mtime, used only for diagnostics.</param>
public sealed record VaultSnapshot(
    byte[] KdfSalt,
    byte[] WrappedDataKey,
    Dictionary<string, byte[]> EncryptedPasswords,
    DateTimeOffset UpdatedAt);

public interface IVaultSync
{
    /// <summary>
    /// Returns Ok(null) when the user has no vault doc yet (first-time setup state).
    /// Returns Ok(snapshot) when one exists. Fail only on transport / auth errors.
    /// </summary>
    Task<Result<VaultSnapshot?>> PullAsync(CancellationToken ct = default);

    Task<Result<bool>> PushAsync(VaultSnapshot snapshot, CancellationToken ct = default);
}
