// FILE: src/GreatEmailApp.Core/Services/WindowsCredentialStore.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
// Direct P/Invoke to advapi32 — no third-party dependency. Targets Generic credentials,
// Local Machine persistence, per-user (Windows enforces this). Each account is keyed by
// a stable target name "GreatEmailApp:{accountId}".

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace GreatEmailApp.Core.Services;

public sealed class WindowsCredentialStore : ICredentialStore
{
    private const string TargetPrefix = "GreatEmailApp:";

    public void Save(string accountId, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("accountId required", nameof(accountId));

        var target = TargetPrefix + accountId;
        var blob = Encoding.Unicode.GetBytes(password ?? "");

        var cred = new CREDENTIAL
        {
            Type = CRED_TYPE.GENERIC,
            TargetName = target,
            UserName = username ?? "",
            CredentialBlob = Marshal.AllocCoTaskMem(blob.Length),
            CredentialBlobSize = (uint)blob.Length,
            Persist = CRED_PERSIST.LOCAL_MACHINE,
            AttributeCount = 0,
            Attributes = IntPtr.Zero,
            Comment = null,
            TargetAlias = null,
        };

        try
        {
            Marshal.Copy(blob, 0, cred.CredentialBlob, blob.Length);
            if (!CredWrite(ref cred, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"CredWrite failed for {target}");
            }
        }
        finally
        {
            if (cred.CredentialBlob != IntPtr.Zero)
                Marshal.FreeCoTaskMem(cred.CredentialBlob);
            // Zero out our local copy of the password.
            Array.Clear(blob, 0, blob.Length);
        }
    }

    public (string Username, string Password)? Read(string accountId)
    {
        var target = TargetPrefix + accountId;
        if (!CredRead(target, CRED_TYPE.GENERIC, 0, out var credPtr))
        {
            var err = Marshal.GetLastWin32Error();
            // 1168 = ERROR_NOT_FOUND
            if (err == 1168) return null;
            throw new Win32Exception(err, $"CredRead failed for {target}");
        }

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            var pwdBytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, pwdBytes, 0, (int)cred.CredentialBlobSize);
            var password = Encoding.Unicode.GetString(pwdBytes);
            Array.Clear(pwdBytes, 0, pwdBytes.Length);
            return (cred.UserName ?? "", password);
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    public void Delete(string accountId)
    {
        var target = TargetPrefix + accountId;
        if (!CredDelete(target, CRED_TYPE.GENERIC, 0))
        {
            var err = Marshal.GetLastWin32Error();
            if (err == 1168) return; // not found, no-op
            throw new Win32Exception(err, $"CredDelete failed for {target}");
        }
    }

    // ── Win32 interop ───────────────────────────────────────────────

    private enum CRED_TYPE : uint
    {
        GENERIC = 1,
    }

    private enum CRED_PERSIST : uint
    {
        SESSION = 1,
        LOCAL_MACHINE = 2,
        ENTERPRISE = 3,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public CRED_TYPE Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public CRED_PERSIST Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, CRED_TYPE type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, CRED_TYPE type, uint flags);

    [DllImport("advapi32", SetLastError = true)]
    private static extern void CredFree(IntPtr cred);
}
