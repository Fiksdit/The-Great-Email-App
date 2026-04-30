// FILE: src/GreatEmailApp/App.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 4
// Changed by: Claude Sonnet 4.6 on behalf of James Reed

using System.Windows;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;
using GreatEmailApp.Core.Storage;
using GreatEmailApp.Services;

namespace GreatEmailApp;

public partial class App : Application
{
    public static ThemeManager Theme { get; } = new();

    // Lightweight service locator — Phase 4 still doesn't need a full DI container.
    public static IImapService           Imap          { get; private set; } = null!;
    public static ICredentialStore       Credentials   { get; private set; } = null!;
    public static IAccountStore          Accounts      { get; private set; } = null!;
    public static ISettingsStore         SettingsStore { get; private set; } = null!;
    public static AppSettings            Settings      { get; set; }         = null!;
    public static IFirebaseAuthService   FirebaseAuth  { get; private set; } = null!;
    public static IFirestoreSyncService  FirestoreSync { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureRoot();

        Imap          = new ImapService();
        Credentials   = new WindowsCredentialStore();
        Accounts      = new JsonAccountStore();
        SettingsStore = new JsonSettingsStore();
        Settings      = SettingsStore.Load();

        var tokenStore = new TokenStore();
        FirebaseAuth  = new FirebaseAuthService(tokenStore);
        FirestoreSync = new FirestoreSyncService(FirebaseAuth);

        Theme.Apply(Settings.Theme, Settings.Accent);

        // Silently pull settings from Firestore if the user is already signed in.
        if (Settings.SyncEnabled && FirebaseAuth.CurrentUser is not null)
            _ = PullAndApplyAsync();
    }

    /// <summary>
    /// Persist AppSettings, re-apply theme/accent, and push to Firestore if signed in.
    /// Called from the Settings dialog and from the first-run overlay.
    /// </summary>
    public static void PersistSettings()
    {
        SettingsStore.Save(Settings);
        Theme.Apply(Settings.Theme, Settings.Accent);

        if (Settings.SyncEnabled && FirebaseAuth.CurrentUser is not null)
            _ = FirestoreSync.PushAsync(Settings, Accounts.LoadAll());
    }

    /// <summary>
    /// Pull settings + accounts from Firestore and merge into local state.
    /// Remote wins for all settings fields; local-only fields (HasShownFirstRun,
    /// SyncEnabled, SignedInEmail) are preserved.
    /// </summary>
    public static async Task PullAndApplyAsync()
    {
        try
        {
            var (remote, remoteAccounts) = await FirestoreSync.PullAsync();
            if (remote is null) return;

            // Preserve local-only flags so the app doesn't re-show the first-run screen.
            remote.SyncEnabled     = Settings.SyncEnabled;
            remote.SignedInEmail   = Settings.SignedInEmail;
            remote.HasShownFirstRun = Settings.HasShownFirstRun;

            Settings = remote;
            SettingsStore.Save(Settings);

            Current.Dispatcher.Invoke(() => Theme.Apply(Settings.Theme, Settings.Accent));

            if (remoteAccounts is { Count: > 0 })
            {
                var local   = Accounts.LoadAll();
                var merged  = MergeAccounts(local, remoteAccounts);
                Accounts.Save(merged);
            }
        }
        catch { /* silently swallow — sync is best-effort */ }
    }

    // Add or update local accounts based on the remote list.
    // Never deletes local accounts so the user doesn't lose data unexpectedly.
    private static List<Account> MergeAccounts(IReadOnlyList<Account> local, List<Account> remote)
    {
        var dict = local.ToDictionary(a => a.Id);
        foreach (var r in remote)
        {
            if (dict.TryGetValue(r.Id, out var existing))
            {
                // Update IMAP/SMTP config but keep runtime state.
                existing.DisplayName    = r.DisplayName;
                existing.ImapHost       = r.ImapHost;
                existing.ImapPort       = r.ImapPort;
                existing.ImapEncryption = r.ImapEncryption;
                existing.SmtpHost       = r.SmtpHost;
                existing.SmtpPort       = r.SmtpPort;
                existing.SmtpEncryption = r.SmtpEncryption;
                existing.Username       = r.Username;
                existing.SyncSettings   = r.SyncSettings;
            }
            else
            {
                dict[r.Id] = r; // new account from another device
            }
        }
        return dict.Values.ToList();
    }
}
