// FILE: src/GreatEmailApp.Core/Auth/DpapiTokenVault.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Security.Cryptography;
using System.Text;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Auth;

/// <summary>
/// Reads/writes the Firebase refresh token to <c>%LOCALAPPDATA%\GreatEmailApp\auth.dat</c>
/// using DPAPI with <see cref="DataProtectionScope.CurrentUser"/> — encryption is
/// keyed to the Windows user, so the file is unreadable by other accounts on the
/// same machine and unrecoverable if copied to another PC. That's intentional.
/// </summary>
public interface ITokenVault
{
    string? Read();
    void Write(string token);
    void Clear();
}

public sealed class DpapiTokenVault : ITokenVault
{
    // Optional entropy mixed into the protect/unprotect so a copied file alone
    // (without the same app) wouldn't decrypt even if DPAPI keys somehow leaked.
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("GreatEmailApp::FirebaseRefreshToken::v1");

    public string? Read()
    {
        var path = AppPaths.AuthDat;
        if (!File.Exists(path)) return null;

        try
        {
            var ciphertext = File.ReadAllBytes(path);
            var plaintext = ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException)
        {
            // File is from another user / corrupt / produced by an older entropy.
            // Treat as absent and remove so we don't keep failing.
            try { File.Delete(path); } catch { }
            return null;
        }
    }

    public void Write(string token)
    {
        AppPaths.EnsureRoot();
        var plaintext = Encoding.UTF8.GetBytes(token);
        var ciphertext = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);

        // Atomic write: tmp + rename, mirroring JsonAccountStore.
        var path = AppPaths.AuthDat;
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, ciphertext);
        File.Move(tmp, path, overwrite: true);
    }

    public void Clear()
    {
        try { if (File.Exists(AppPaths.AuthDat)) File.Delete(AppPaths.AuthDat); } catch { }
    }
}
