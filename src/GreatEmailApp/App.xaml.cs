// FILE: src/GreatEmailApp/App.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 9
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.IO;
using System.Windows;
using GreatEmailApp.Core.Auth;
using GreatEmailApp.Core.Config;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Notifications;
using GreatEmailApp.Core.Rules;
using GreatEmailApp.Core.Search;
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
    public static ISmtpService Smtp { get; private set; } = null!;
    public static ICredentialStore Credentials { get; private set; } = null!;
    public static IAccountStore Accounts { get; private set; } = null!;
    public static IContactsStore Contacts { get; private set; } = null!;
    public static IRulesStore Rules { get; private set; } = null!;
    public static IRulesEngine RulesEngine { get; private set; } = null!;
    public static IRuleSuggestionEngine RuleSuggestions { get; private set; } = null!;
    public static IFolderCache FolderCache { get; private set; } = null!;
    public static ISettingsStore SettingsStore { get; private set; } = null!;
    public static AppSettings Settings { get; set; } = null!;
    public static AppConfig Config { get; private set; } = null!;
    public static IAuthService Auth { get; private set; } = null!;
    public static IFirestoreSyncService Sync { get; private set; } = null!;
    public static SyncCoordinator SyncCoordinator { get; private set; } = null!;
    public static IUpdateService Updates { get; private set; } = null!;
    public static IUpdateInstaller UpdateInstaller { get; private set; } = null!;
    public static INewMailPoller MailPoller { get; private set; } = null!;
    public static IMessageCache MessageCache { get; private set; } = null!;
    private static TrayNotifier? _tray;

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

        // Permanent crash logger — writes any unhandled exception to
        // %LOCALAPPDATA%\GreatEmailApp\crash.log so post-mortem doesn't depend
        // on Windows Error Reporting bucket guessing. Cheap insurance.
        DispatcherUnhandledException += (_, ex) =>
        {
            LogCrash("DispatcherUnhandled", ex.Exception);
            ex.Handled = false; // still let the process die — we want the WER too
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            LogCrash("AppDomainUnhandled", ex.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            LogCrash("UnobservedTask", ex.Exception);
            ex.SetObserved();
        };

        Config = AppConfig.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Imap = new ImapService();
        Smtp = new SmtpService();
        Credentials = new WindowsCredentialStore();
        Accounts = new JsonAccountStore();
        Contacts = new JsonContactsStore();
        Rules = new JsonRulesStore();
        FolderCache = new JsonFolderCache();
        SettingsStore = new JsonSettingsStore();
        Settings = SettingsStore.Load();
        Auth = new FirebaseAuthService(Config, new DpapiTokenVault());
        Sync = new FirestoreSyncService(Config, Auth);
        SyncCoordinator = new SyncCoordinator(Settings, SettingsStore, Accounts, Contacts, Rules, Auth, Sync);
        SyncCoordinator.RemotePullApplied += OnRemotePullApplied;
        Updates = new GitHubUpdateService();
        UpdateInstaller = new UpdateInstaller();
        MessageCache = new SqliteMessageCache();
        _ = MessageCache.InitAsync();
        MailPoller = new NewMailPoller(Settings, Accounts, Credentials, Imap);
        // Indexing piggybacks on the poller — every poll cycle that fetches
        // envelopes for a notification check also writes them into the cache,
        // so search has fresh data with no extra IMAP round-trips.
        MailPoller.MessagesPolled += async (_, ev) =>
        {
            try { await MessageCache.UpsertEnvelopesAsync(ev.Account.Id, ev.Account.EmailAddress, ev.FolderPath, ev.Messages); }
            catch { }
        };
        _tray = new TrayNotifier(MailPoller);
        MailPoller.Start();

        // Rules engine: subscribes to MessagesPolled so every poll cycle that
        // pulls fresh inbox state runs enabled rules against the new mail.
        RulesEngine = new RulesEngine(Rules, Accounts, Credentials, Imap, MailPoller);
        RulesEngine.Start();
        RuleSuggestions = new RuleSuggestionEngine(MessageCache, Rules);
        Exit += (_, _) => { _tray?.Dispose(); (MailPoller as IDisposable)?.Dispose(); };

        Theme.Apply(Settings.Theme, Settings.Accent);

        // Silent re-auth → SyncCoordinator picks up SessionChanged and pulls.
        _ = RestoreAndStartSyncAsync();

        // Silent update probe. Result is logged via UpdateAvailable for UI to
        // surface a badge later; here we just warm the cache so the About tab
        // shows results instantly when the user opens it.
        _ = CheckForUpdatesSilentAsync();
    }

    private static async Task RestoreAndStartSyncAsync()
    {
        // SessionChanged fires inside TryRestoreAsync if it succeeds; the
        // coordinator subscribes to that and pulls. StartAsync covers the case
        // where the session is already restored (e.g. fast restart).
        await Auth.TryRestoreAsync();
        await SyncCoordinator.StartAsync();
    }

    private static void OnRemotePullApplied(object? sender, EventArgs e)
    {
        // Marshal to UI thread — the coordinator can fire from a background
        // pull task. Re-applying the theme picks up Theme/Accent changes;
        // MainViewModel.ReloadAccounts pulls the freshly-saved accounts.json.
        Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            Theme.Apply(Settings.Theme, Settings.Accent);
            if (Current.MainWindow?.DataContext is ViewModels.MainViewModel mvm)
                mvm.ReloadAccounts();
        }));
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
    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var path = Path.Combine(AppPaths.Root, "crash.log");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source}: {ex}\n\n";
            File.AppendAllText(path, line);
        }
        catch { /* logging is best-effort */ }
    }

    public static void PersistSettings()
    {
        SettingsStore.Save(Settings); // Saved event → SyncCoordinator debounced push.
        Theme.Apply(Settings.Theme, Settings.Accent);
    }
}
