// FILE: src/GreatEmailApp.Core/Crypto/PasswordVault.cs
// Created: 2026-05-07 | Revised: 2026-05-07 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Crypto core for the password sync vault. Implements:
//
//   1. Master passphrase  --[Argon2id]-->  KEK (key-encryption key)
//   2. Per-user random data key (256-bit AES) wrapped by the KEK with AES-GCM.
//   3. Each IMAP password encrypted with the data key using AES-GCM.
//
// Why a wrapped data key instead of just deriving from the passphrase directly?
// Because we want the user to be able to *change* their passphrase without having
// to re-encrypt every IMAP password — only the wrapped data key needs rewrapping.
//
// Argon2id parameters (OWASP 2024 recommendations, "second-choice" profile):
//   memory:      64 MiB
//   iterations:  3
//   parallelism: 4
//   salt:        16 bytes random per user
//   output:      32 bytes (used directly as the AES-256 KEK)
//
// AES-GCM tags are 16 bytes, nonces are 12 bytes; we generate a fresh random
// nonce per encryption (data key wrap, every password). Nonce reuse with the
// same key is catastrophic for GCM, so this matters.

using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace GreatEmailApp.Core.Crypto;

/// <summary>
/// Stateless crypto helpers for the password vault. The wrapped data key
/// (and KDF salt + params) is held by callers and persisted to Firestore.
/// The unwrapped data key is held in memory only for the lifetime of the
/// process, plus a DPAPI-encrypted local cache (see <c>LocalDataKeyCache</c>).
/// </summary>
public static class PasswordVault
{
    public const int DataKeyBytes = 32;       // AES-256
    public const int KdfSaltBytes = 16;
    public const int GcmNonceBytes = 12;
    public const int GcmTagBytes = 16;

    public const int Argon2MemoryKb = 64 * 1024;
    public const int Argon2Iterations = 3;
    public const int Argon2Parallelism = 4;

    /// <summary>Generate a fresh random data key. Caller wraps it via <see cref="WrapDataKey"/>.</summary>
    public static byte[] NewDataKey()
    {
        var k = new byte[DataKeyBytes];
        RandomNumberGenerator.Fill(k);
        return k;
    }

    /// <summary>Generate a fresh KDF salt. One per user, persisted in the Firestore vault doc.</summary>
    public static byte[] NewKdfSalt()
    {
        var s = new byte[KdfSaltBytes];
        RandomNumberGenerator.Fill(s);
        return s;
    }

    /// <summary>
    /// Derive the KEK from the master passphrase via Argon2id. This is the
    /// expensive call — ~150–500ms on a typical desktop. Run off the UI thread.
    /// </summary>
    public static byte[] DeriveKek(string passphrase, byte[] salt)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);
        if (salt is null || salt.Length == 0) throw new ArgumentException("Salt required.", nameof(salt));

        using var argon = new Argon2id(Encoding.UTF8.GetBytes(passphrase))
        {
            Salt = salt,
            DegreeOfParallelism = Argon2Parallelism,
            MemorySize = Argon2MemoryKb,
            Iterations = Argon2Iterations,
        };
        return argon.GetBytes(DataKeyBytes);
    }

    /// <summary>
    /// Wrap (encrypt) the data key with the KEK using AES-GCM. Layout:
    /// <c>nonce(12) || ciphertext(N) || tag(16)</c>. The whole blob is what
    /// gets stored in the Firestore vault doc.
    /// </summary>
    public static byte[] WrapDataKey(byte[] dataKey, byte[] kek)
        => AesGcmEncrypt(dataKey, kek, associatedData: null);

    /// <summary>Unwrap the data key with the KEK. Returns null on auth-tag mismatch (wrong passphrase).</summary>
    public static byte[]? UnwrapDataKey(byte[] wrappedDataKey, byte[] kek)
        => AesGcmDecrypt(wrappedDataKey, kek, associatedData: null);

    /// <summary>
    /// Encrypt an IMAP password (or any string secret) with the data key.
    /// AAD is the accountId, which means swapping ciphertexts between accounts
    /// will fail to decrypt — a small defense against vault-doc-tampering.
    /// </summary>
    public static byte[] EncryptPassword(string plaintext, byte[] dataKey, string accountId)
    {
        var pt = Encoding.UTF8.GetBytes(plaintext);
        return AesGcmEncrypt(pt, dataKey, Encoding.UTF8.GetBytes(accountId));
    }

    /// <summary>Decrypt a password. Returns null on tampering / wrong key / wrong accountId.</summary>
    public static string? DecryptPassword(byte[] ciphertext, byte[] dataKey, string accountId)
    {
        var pt = AesGcmDecrypt(ciphertext, dataKey, Encoding.UTF8.GetBytes(accountId));
        return pt is null ? null : Encoding.UTF8.GetString(pt);
    }

    // --------------------------------------------------------------------- //
    // AES-GCM helpers
    // --------------------------------------------------------------------- //

    private static byte[] AesGcmEncrypt(byte[] plaintext, byte[] key, byte[]? associatedData)
    {
        var nonce = new byte[GcmNonceBytes];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[GcmTagBytes];

        using var aes = new AesGcm(key, GcmTagBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        // Layout: nonce || ciphertext || tag
        var blob = new byte[GcmNonceBytes + ciphertext.Length + GcmTagBytes];
        Buffer.BlockCopy(nonce, 0, blob, 0, GcmNonceBytes);
        Buffer.BlockCopy(ciphertext, 0, blob, GcmNonceBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, blob, GcmNonceBytes + ciphertext.Length, GcmTagBytes);
        return blob;
    }

    private static byte[]? AesGcmDecrypt(byte[] blob, byte[] key, byte[]? associatedData)
    {
        if (blob is null || blob.Length < GcmNonceBytes + GcmTagBytes) return null;

        var nonce = new byte[GcmNonceBytes];
        var tag = new byte[GcmTagBytes];
        var ctLen = blob.Length - GcmNonceBytes - GcmTagBytes;
        var ciphertext = new byte[ctLen];

        Buffer.BlockCopy(blob, 0, nonce, 0, GcmNonceBytes);
        Buffer.BlockCopy(blob, GcmNonceBytes, ciphertext, 0, ctLen);
        Buffer.BlockCopy(blob, GcmNonceBytes + ctLen, tag, 0, GcmTagBytes);

        var plaintext = new byte[ctLen];
        try
        {
            using var aes = new AesGcm(key, GcmTagBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            return plaintext;
        }
        catch (CryptographicException)
        {
            // Wrong key, tampered ciphertext, or wrong associatedData.
            return null;
        }
    }
}
