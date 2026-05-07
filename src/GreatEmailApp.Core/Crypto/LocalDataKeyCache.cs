// FILE: src/GreatEmailApp.Core/Crypto/LocalDataKeyCache.cs
// Created: 2026-05-07 | Revised: 2026-05-07 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Persists the unwrapped per-user vault data key locally so the user only has
// to type the master passphrase the first time on a PC (and on any explicit
// "resync passwords" call). DPAPI-encrypted with CurrentUser scope, so the
// file is unreadable by other Windows accounts on the same box and bricked
// if copied to another machine. By design.
//
// Modeled on DpapiTokenVault (auth.dat). Same atomic-write pattern.

using System.Security.Cryptography;
using System.Text;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Crypto;

public interface ILocalDataKeyCache
{
    /// <summary>Returns the cached data key, or null if absent / unreadable.</summary>
    byte[]? Read();
    void Write(byte[] dataKey);
    void Clear();
}

public sealed class DpapiLocalDataKeyCache : ILocalDataKeyCache
{
    // Entropy is namespaced + versioned so we can rotate without colliding
    // with auth.dat's entropy and so old cache files don't accidentally
    // decrypt under a future format change.
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("GreatEmailApp::VaultDataKey::v1");

    public byte[]? Read()
    {
        var path = AppPaths.VaultDat;
        if (!File.Exists(path)) return null;

        try
        {
            var ciphertext = File.ReadAllBytes(path);
            return ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            // File is from another user / corrupt / produced by older entropy.
            // Treat as absent and remove so we don't keep failing.
            try { File.Delete(path); } catch { }
            return null;
        }
    }

    public void Write(byte[] dataKey)
    {
        if (dataKey is null || dataKey.Length == 0)
            throw new ArgumentException("Data key cannot be empty.", nameof(dataKey));

        AppPaths.EnsureRoot();
        var ciphertext = ProtectedData.Protect(dataKey, Entropy, DataProtectionScope.CurrentUser);

        var path = AppPaths.VaultDat;
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, ciphertext);
        File.Move(tmp, path, overwrite: true);
    }

    public void Clear()
    {
        try { if (File.Exists(AppPaths.VaultDat)) File.Delete(AppPaths.VaultDat); } catch { }
    }
}
