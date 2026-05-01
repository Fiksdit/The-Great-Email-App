// FILE: src/GreatEmailApp.Core/Storage/AppPaths.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 3
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Storage;

/// <summary>
/// Centralized paths for app-local data. Everything lives under
/// %LOCALAPPDATA%\GreatEmailApp\ so it's per-user, not roamed.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GreatEmailApp");

    public static string AccountsJson => Path.Combine(Root, "accounts.json");
    public static string SettingsJson => Path.Combine(Root, "settings.json");
    public static string CacheDb     => Path.Combine(Root, "cache.db");
    public static string LogsFolder  => Path.Combine(Root, "logs");

    /// <summary>
    /// Encrypted Firebase Auth refresh token (DPAPI, current-user scope).
    /// Written by IAuthService after a successful Google sign-in. Per rulebook §7A.
    /// </summary>
    public static string AuthDat     => Path.Combine(Root, "auth.dat");

    /// <summary>
    /// SyncCoordinator's local bookkeeping (last-pushed timestamp, etc).
    /// Used to detect "local has unpushed changes" so a stale cloud pull
    /// doesn't clobber edits the user just made — see FIX-2026-04-30-002.
    /// </summary>
    public static string SyncMetaJson => Path.Combine(Root, "sync-meta.json");

    /// <summary>
    /// Per-account "last-seen UID" snapshot for the new-mail poller.
    /// Local-only — knowing which messages this PC has already notified about
    /// is per-machine state, not synced.
    /// </summary>
    public static string NotificationsStateJson => Path.Combine(Root, "notifications-state.json");

    /// <summary>Ensures the root folder exists. Safe to call repeatedly.</summary>
    public static void EnsureRoot()
    {
        if (!Directory.Exists(Root))
            Directory.CreateDirectory(Root);
    }
}
