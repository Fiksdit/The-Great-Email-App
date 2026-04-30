// FILE: src/GreatEmailApp/App.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 7
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.IO;
using System.Windows;
using GreatEmailApp.Core.Auth;
using GreatEmailApp.Core.Config;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;
using GreatEmailApp.Core.Storage;
using GreatEmailApp.Core.Sync;
using GreatEmailApp.Core.Updates;
using GreatEmailApp.Services;

namespace GreatEmailApp;

public partial class App : Application
{
    public static ThemeManager Theme { get; } = new();

    // Lightweight service locator — Phase 2 doesn't need a full DI container.
    public static IImapService Imap { get; private set; } = null!;
    public static ICredentialStore Credentials { get; private set; } = null!;
    public static IAccountStore Accounts { get; private set; } = null!;
    public static ISettingsStore SettingsStore { get; private set; } = null!;
    public static AppSettings Settings { get; set; } = null!;
    public static AppConfig Config { get; private set; } = null!;
    public static IAuthService Auth { get; private set; } = null!;
    public static IFirestoreSyncService Sync { get; private set; } = null!;
    public static IUpdateService Updates { get; private set; } = null!;
    public static IUpdateInstaller UpdateInstaller { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Force software rendering. Hardware-accelerated WPF on certain GPU /
        // driver combinations renders pure-white windows even though the visual
        // tree, theme dictionaries, and DynamicResource lookups all succeed
        // (FIX-2026-04-30-001 — reproduced on a fresh Win11 box, Tier 2 GPU).
        // SoftwareOnly is plenty fast for an email client and removes a whole
        // class of GPU-driver-dependent bugs across our target install base.
        System.Windows.Media.RenderOptions.ProcessRenderMode =
            System.Windows.Interop.RenderMode.SoftwareOnly;

        base.OnStartup(e);

        AppPaths.EnsureRoot();

        Config = AppConfig.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Imap = new ImapService();
        Credentials = new WindowsCredentialStore();
        Accounts = new JsonAccountStore();
        SettingsStore = new JsonSettingsStore();
        Settings = SettingsStore.Load();
        Auth = new FirebaseAuthService(Config, new DpapiTokenVault());
        Sync = new FirestoreSyncService(Config, Auth);
        Updates = new GitHubUpdateService();
        UpdateInstaller = new UpdateInstaller();

        Theme.Apply(Settings.Theme, Settings.Accent);

        // Best-effort silent re-auth from the encrypted refresh token. Fire and
        // forget — UI stays usable whether this succeeds or not.
        _ = Auth.TryRestoreAsync();

        // Silent update probe. Result is logged via UpdateAvailable for UI to
        // surface a badge later; here we just warm the cache so the About tab
        // shows results instantly when the user opens it.
        _ = CheckForUpdatesSilentAsync();
    }

    private static async Task CheckForUpdatesSilentAsync()
    {
        try
        {
            var result = await Updates.CheckAsync();
            if (result is Core.Services.Result<Core.Updates.UpdateInfo?>.Ok ok && ok.Value is not null)
                LatestUpdateInfo = ok.Value;
        }
        catch { /* best effort */ }
    }

    /// <summary>Cached most-recent silent-check result. Null if up to date or unknown.</summary>
    public static Core.Updates.UpdateInfo? LatestUpdateInfo { get; private set; }

    /// <summary>
    /// Persist AppSettings and re-apply any UI-affecting changes (theme/accent).
    /// Called from the Settings dialog after the user clicks Apply/Save.
    /// </summary>
    public static void PersistSettings()
    {
        SettingsStore.Save(Settings);
        Theme.Apply(Settings.Theme, Settings.Accent);
    }
}
