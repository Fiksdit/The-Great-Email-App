// FILE: src/GreatEmailApp/Services/ToastAumid.cs
// Created: 2026-05-13 | Revised: 2026-05-13 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Registers the process's AppUserModelID and ensures a Start-menu shortcut
// exists with the matching AUMID property set. Required for Windows 10/11
// toast notifications on a non-MSIX desktop app: without that pairing, every
// toast activation falls back to Windows' "Look for an app in the Microsoft
// Store to open this link" popup. The fix log calls out this exact symptom
// (FIX-2026-05-12-002 and the follow-up FIX-2026-05-13-002).
//
// What this file does NOT do:
//   - Register a COM toast activator (so clicking a toast won't launch the
//     app back). For now the toast simply displays; click is a no-op. That
//     is acceptable — the goal here is to stop the Store popup, not to add
//     toast-click deep-linking. The COM activator + IServerSecurity dance
//     can land later behind its own decision-log entry.
//
// Reference:
//   https://learn.microsoft.com/en-us/windows/win32/shell/appids
//   https://learn.microsoft.com/en-us/windows/apps/design/shell/tiles-and-notifications/send-local-toast-other-apps

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace GreatEmailApp.Services;

internal static class ToastAumid
{
    /// <summary>Process-wide AppUserModelID. Stable; do not change between
    /// releases or existing Start-menu shortcuts will orphan.</summary>
    public const string AppId = "Fiksdit.TheGreatEmailApp";

    private const string ShortcutFileName = "The Great Email App.lnk";

    /// <summary>
    /// Idempotent. Sets the process AUMID and writes a Start-menu shortcut
    /// pointing at the running exe with PKEY_AppUserModel_ID set. Safe to call
    /// every startup — the shortcut is only rewritten when it's missing or its
    /// target path doesn't match the current exe (e.g. user moved the install).
    /// </summary>
    public static void EnsureRegistered()
    {
        try { SetCurrentProcessExplicitAppUserModelID(AppId); }
        catch { /* best effort — older Windows or unusual host */ }

        try { EnsureStartMenuShortcut(); }
        catch { /* shortcut is best-effort; without it toasts may still trip the popup */ }
    }

    // --------------------------------------------------------------------- //
    // Shortcut maintenance
    // --------------------------------------------------------------------- //

    private static void EnsureStartMenuShortcut()
    {
        var startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs");
        Directory.CreateDirectory(startMenu);
        var linkPath = Path.Combine(startMenu, ShortcutFileName);

        var exe = Path.Combine(AppContext.BaseDirectory, "GreatEmailApp.exe");
        if (!File.Exists(exe)) return;  // running unpacked or under test — nothing to point at

        if (File.Exists(linkPath) && ShortcutTargetMatches(linkPath, exe)) return;

        CreateShortcutWithAumid(linkPath, exe, AppId);
    }

    private static bool ShortcutTargetMatches(string linkPath, string targetExe)
    {
        IShellLinkW? shellLink = null;
        try
        {
            shellLink = (IShellLinkW)new CShellLink();
            ((IPersistFile)shellLink).Load(linkPath, 0);
            var sb = new StringBuilder(260);
            shellLink.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            return string.Equals(sb.ToString(), targetExe, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
        finally
        {
            if (shellLink is not null) Marshal.FinalReleaseComObject(shellLink);
        }
    }

    private static void CreateShortcutWithAumid(string linkPath, string targetExe, string aumid)
    {
        var shellLink = (IShellLinkW)new CShellLink();
        try
        {
            shellLink.SetPath(targetExe);
            shellLink.SetWorkingDirectory(Path.GetDirectoryName(targetExe) ?? "");
            shellLink.SetIconLocation(targetExe, 0);
            shellLink.SetDescription("The Great Email App");

            // PKEY_AppUserModel_ID — this is the bit Windows toast routing reads
            // to associate a non-MSIX exe with an AUMID. Without it, the modern
            // toast pipeline can't find the calling app and falls back to the
            // "Look for an app in the Microsoft Store" popup.
            var store = (IPropertyStore)shellLink;
            var key = new PROPERTYKEY
            {
                fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
                pid = 5, // PKEY_AppUserModel_ID
            };
            var pv = new PROPVARIANT
            {
                vt = VT_LPWSTR,
                pwszVal = Marshal.StringToCoTaskMemUni(aumid),
            };
            try
            {
                store.SetValue(ref key, ref pv);
                store.Commit();
            }
            finally { _ = PropVariantClear(ref pv); }

            ((IPersistFile)shellLink).Save(linkPath, true);
        }
        finally { Marshal.FinalReleaseComObject(shellLink); }
    }

    // --------------------------------------------------------------------- //
    // COM interop
    // --------------------------------------------------------------------- //

    [DllImport("shell32.dll", PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appID);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    private const ushort VT_LPWSTR = 31;

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    // PROPVARIANT for VT_LPWSTR only. The full PROPVARIANT union is 16 bytes on
    // x64 (8 header + 16 union = 24 total). We use just the pwszVal pointer (8
    // bytes) and zero the rest. unionTail must exist so the layout matches what
    // COM expects, otherwise SetValue stomps on whatever follows the struct.
    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr pwszVal;   // first half of the 16-byte union
        public IntPtr unionTail; // second half — keep zero for VT_LPWSTR
    }
}
