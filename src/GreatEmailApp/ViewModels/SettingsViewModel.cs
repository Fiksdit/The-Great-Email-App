// FILE: src/GreatEmailApp/ViewModels/SettingsViewModel.cs
// Created: 2026-04-29 | Revised: 2026-06-12 | Rev: 6
// Changed by: Claude Opus 4.8 on behalf of James Reed

using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreatEmailApp.Core.Auth;
using GreatEmailApp.Core.Crypto;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;
using GreatEmailApp.Core.Spam;
using GreatEmailApp.Core.Sync;
using GreatEmailApp.Core.Updates;
using GreatEmailApp.Views.Dialogs;

namespace GreatEmailApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IAccountStore _accountStore;
    private readonly IContactsStore _contactsStore;
    private readonly IRulesStore _rulesStore;
    private readonly ISpamConfigStore _spamStore;
    private readonly SpamFilterConfig _spamConfig;
    private readonly ICredentialStore _creds;
    private readonly ISettingsStore _settingsStore;
    private readonly IAuthService _auth;
    private readonly IFirestoreSyncService _sync;
    private readonly SyncCoordinator _coordinator;
    private readonly IUpdateService _updates;
    private readonly IUpdateInstaller _installer;
    private readonly VaultManager _vault;

    [ObservableProperty] private string activeTab = "Appearance";

    [ObservableProperty] private AppTheme theme;
    [ObservableProperty] private string accent = "#3A6FF8";
    [ObservableProperty] private DensityMode density;
    [ObservableProperty] private RibbonStyle ribbon;

    [ObservableProperty] private bool showHtml;
    [ObservableProperty] private bool allowRemoteImages;
    [ObservableProperty] private bool enableNewMailNotifications;
    [ObservableProperty] private int markReadDelaySeconds;
    [ObservableProperty] private int syncIntervalMinutes;

    // --- Sync section state ---
    [ObservableProperty] private bool isSignedIn;
    [ObservableProperty] private string signedInEmail = "";
    [ObservableProperty] private string syncStatus = "";
    [ObservableProperty] private bool isSyncBusy;

    // --- Password vault state ---
    // VaultStatusText is the human label (e.g. "Not set up", "Locked on this PC", "Unlocked").
    // VaultIsUnlocked / VaultIsLocked / VaultIsNotSetUp drive button visibility via converters.
    [ObservableProperty] private string vaultStatusText = "Checking…";
    [ObservableProperty] private bool isVaultBusy;
    [ObservableProperty] private bool vaultIsNotSetUp;
    [ObservableProperty] private bool vaultIsLocked;
    [ObservableProperty] private bool vaultIsUnlocked;

    // --- Updates / About state ---
    public string CurrentVersionText { get; } = $"v{GitHubUpdateService.CurrentVersion()}";
    [ObservableProperty] private string updateStatus = "";
    [ObservableProperty] private bool isUpdateBusy;
    [ObservableProperty] private UpdateInfo? availableUpdate;
    public bool HasUpdate => AvailableUpdate is not null;
    partial void OnAvailableUpdateChanged(UpdateInfo? value)
    {
        OnPropertyChanged(nameof(HasUpdate));
        InstallUpdateCommand.NotifyCanExecuteChanged();
    }

    public string[] Accents { get; } = new[]
    {
        "#3A6FF8", "#14a37f", "#8a5cf5", "#0ea5e9", "#d29014", "#d4406b",
    };

    public ObservableCollection<Account> ManagedAccounts { get; } = new();
    public ObservableCollection<Contact> ManagedContacts { get; } = new();
    [ObservableProperty] private Contact? selectedContact;
    [ObservableProperty] private string newContactName = "";
    [ObservableProperty] private string newContactEmail = "";

    // --- Rules state ---
    public ObservableCollection<MailRule> ManagedRules { get; } = new();

    // --- Spam filter state ---
    // SpamEnabled / SpamThreshold map to SpamFilterConfig; ShowJunkUnreadBadge
    // is an AppSettings flag (UI concern) but lives on the Spam tab because
    // that's where users think about junk. The three string lists are the
    // editable keyword / blocked / trusted sets, mirrored into _spamConfig on
    // save (SaveSpamConfig, called from the dialog's Close path).
    [ObservableProperty] private bool spamEnabled;
    [ObservableProperty] private int spamThreshold;
    [ObservableProperty] private bool showJunkUnreadBadge;
    [ObservableProperty] private string newSpamKeyword = "";
    [ObservableProperty] private string newBlockedSender = "";
    [ObservableProperty] private string newTrustedSender = "";
    [ObservableProperty] private string spamSenderError = "";
    public bool HasSpamSenderError => !string.IsNullOrEmpty(SpamSenderError);
    partial void OnSpamSenderErrorChanged(string value) => OnPropertyChanged(nameof(HasSpamSenderError));
    public ObservableCollection<string> SpamKeywords { get; } = new();
    public ObservableCollection<string> BlockedSenders { get; } = new();
    public ObservableCollection<string> TrustedSenders { get; } = new();

    public IAsyncRelayCommand SignInCommand { get; }
    public IAsyncRelayCommand SignOutCommand { get; }
    public IAsyncRelayCommand SyncNowCommand { get; }
    public IAsyncRelayCommand SetUpVaultCommand { get; }
    public IAsyncRelayCommand UnlockVaultCommand { get; }
    public IAsyncRelayCommand ResyncPasswordsCommand { get; }
    public IAsyncRelayCommand CheckForUpdatesCommand { get; }
    public IAsyncRelayCommand InstallUpdateCommand { get; }

    public SettingsViewModel(
        AppSettings settings,
        IAccountStore accountStore,
        IContactsStore contactsStore,
        IRulesStore rulesStore,
        ISpamConfigStore spamStore,
        ICredentialStore creds,
        ISettingsStore settingsStore,
        IAuthService auth,
        IFirestoreSyncService sync,
        SyncCoordinator coordinator,
        IUpdateService updates,
        IUpdateInstaller installer,
        VaultManager vault)
    {
        _settings = settings;
        _accountStore = accountStore;
        _contactsStore = contactsStore;
        _rulesStore = rulesStore;
        _spamStore = spamStore;
        _creds = creds;
        _settingsStore = settingsStore;
        _auth = auth;
        _sync = sync;
        _coordinator = coordinator;
        _updates = updates;
        _installer = installer;
        _vault = vault;
        _coordinator.StateChanged += OnCoordinatorStateChanged;

        theme = settings.Theme;
        accent = settings.Accent;
        density = settings.Density;
        ribbon = settings.Ribbon;
        showHtml = settings.ShowHtml;
        allowRemoteImages = settings.AllowRemoteImages;
        enableNewMailNotifications = settings.EnableNewMailNotifications;
        markReadDelaySeconds = settings.MarkReadDelaySeconds;
        syncIntervalMinutes = settings.SyncIntervalMinutes;

        foreach (var a in _accountStore.LoadAll())
            ManagedAccounts.Add(a);
        foreach (var c in _contactsStore.LoadAll().OrderBy(x => x.DisplayName))
            ManagedContacts.Add(c);
        foreach (var r in _rulesStore.LoadAll())
            ManagedRules.Add(r);

        // Spam config — load once, hydrate the editable collections + toggles.
        // ShowJunkUnreadBadge comes from AppSettings, not the spam config.
        _spamConfig = _spamStore.Load();
        spamEnabled = _spamConfig.Enabled;
        spamThreshold = _spamConfig.ThresholdScore;
        showJunkUnreadBadge = settings.ShowJunkUnreadBadge;
        foreach (var k in _spamConfig.SubjectKeywords.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            SpamKeywords.Add(k);
        foreach (var s in _spamConfig.BlockedSenders.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            BlockedSenders.Add(s);
        foreach (var s in _spamConfig.TrustedSenders.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            TrustedSenders.Add(s);

        SignInCommand           = new AsyncRelayCommand(SignInAsync,         () => !IsSyncBusy && !IsSignedIn);
        SignOutCommand          = new AsyncRelayCommand(SignOutAsync,        () => !IsSyncBusy &&  IsSignedIn);
        SyncNowCommand          = new AsyncRelayCommand(SyncNowAsync,        () => !IsSyncBusy &&  IsSignedIn);
        SetUpVaultCommand       = new AsyncRelayCommand(SetUpVaultAsync,     () => !IsVaultBusy && IsSignedIn && VaultIsNotSetUp);
        UnlockVaultCommand      = new AsyncRelayCommand(UnlockVaultAsync,    () => !IsVaultBusy && IsSignedIn && VaultIsLocked);
        ResyncPasswordsCommand  = new AsyncRelayCommand(UnlockVaultAsync,    () => !IsVaultBusy && IsSignedIn && (VaultIsLocked || VaultIsUnlocked));
        CheckForUpdatesCommand  = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsUpdateBusy);
        InstallUpdateCommand    = new AsyncRelayCommand(InstallUpdateAsync,   () => !IsUpdateBusy && AvailableUpdate is not null);

        _auth.SessionChanged += OnSessionChanged;
        ApplySession(_auth.Current);
    }

    partial void OnIsVaultBusyChanged(bool value) => RefreshVaultCommands();
    partial void OnVaultIsNotSetUpChanged(bool value) => RefreshVaultCommands();
    partial void OnVaultIsLockedChanged(bool value) => RefreshVaultCommands();
    partial void OnVaultIsUnlockedChanged(bool value) => RefreshVaultCommands();

    private void RefreshVaultCommands()
    {
        SetUpVaultCommand.NotifyCanExecuteChanged();
        UnlockVaultCommand.NotifyCanExecuteChanged();
        ResyncPasswordsCommand.NotifyCanExecuteChanged();
    }

    partial void OnThemeChanged(AppTheme value)   { _settings.Theme = value;   ApplyLive(); }
    partial void OnAccentChanged(string value)    { _settings.Accent = value;  ApplyLive(); }
    partial void OnDensityChanged(DensityMode v)  => _settings.Density = v;
    partial void OnRibbonChanged(RibbonStyle v)   => _settings.Ribbon = v;
    partial void OnShowHtmlChanged(bool v)        => _settings.ShowHtml = v;
    partial void OnAllowRemoteImagesChanged(bool v) => _settings.AllowRemoteImages = v;
    partial void OnEnableNewMailNotificationsChanged(bool v) => _settings.EnableNewMailNotifications = v;
    partial void OnMarkReadDelaySecondsChanged(int v) => _settings.MarkReadDelaySeconds = v;
    partial void OnSyncIntervalMinutesChanged(int v)
    {
        // Clamp to >=1. The NewMailPoller already clamps internally as a
        // safety net, but enforcing here means the value persisted to
        // settings.json (and synced to Firestore) matches what actually runs.
        // Recursive set terminates after one bounce since v == 1 second time.
        if (v < 1) { SyncIntervalMinutes = 1; return; }
        _settings.SyncIntervalMinutes = v;
    }

    // ----- Spam tab ----- //

    partial void OnSpamThresholdChanged(int v)
    {
        // Clamp to [0, 100]. One-bounce recursion terminates (clamped value is
        // already in range the second time through).
        if (v < 0)   { SpamThreshold = 0;   return; }
        if (v > 100) { SpamThreshold = 100; return; }
    }

    partial void OnShowJunkUnreadBadgeChanged(bool v)
    {
        _settings.ShowJunkUnreadBadge = v;
        // Repaint the sidebar Junk chip immediately — no restart needed.
        if (Application.Current.MainWindow?.DataContext is MainViewModel mvm)
            mvm.RefreshAllFolderBadges();
    }

    [RelayCommand]
    private void AddSpamKeyword(string? keyword)
    {
        var k = (keyword ?? "").Trim();
        if (k.Length == 0) return;
        if (SpamKeywords.Any(x => x.Equals(k, StringComparison.OrdinalIgnoreCase))) return;
        SpamKeywords.Add(k);
        NewSpamKeyword = "";
    }

    [RelayCommand]
    private void RemoveSpamKeyword(string? keyword)
    {
        if (keyword is null) return;
        var match = SpamKeywords.FirstOrDefault(x => x.Equals(keyword, StringComparison.OrdinalIgnoreCase));
        if (match is not null) SpamKeywords.Remove(match);
    }

    [RelayCommand]
    private void RestoreDefaultKeywords()
    {
        // Re-merge the shipping defaults; user additions are preserved.
        foreach (var k in SpamFilterConfig.BuiltInKeywords)
        {
            if (!SpamKeywords.Any(x => x.Equals(k, StringComparison.OrdinalIgnoreCase)))
                SpamKeywords.Add(k);
        }
    }

    [RelayCommand]
    private void AddBlockedSender(string? sender) => AddSender(sender, BlockedSenders);

    [RelayCommand]
    private void RemoveBlockedSender(string? sender) => RemoveSender(sender, BlockedSenders);

    [RelayCommand]
    private void AddTrustedSender(string? sender) => AddSender(sender, TrustedSenders);

    [RelayCommand]
    private void RemoveTrustedSender(string? sender) => RemoveSender(sender, TrustedSenders);

    private void AddSender(string? sender, ObservableCollection<string> target)
    {
        var s = (sender ?? "").Trim();
        if (s.Length == 0) return;
        // Accept a full address (foo@bar.com) or a domain wildcard (@bar.com).
        // Anything without an '@' is rejected.
        if (!s.Contains('@'))
        {
            SpamSenderError = $"\"{s}\" isn't a valid address or @domain.";
            return;
        }
        SpamSenderError = "";
        if (target.Any(x => x.Equals(s, StringComparison.OrdinalIgnoreCase))) return;
        target.Add(s);
        NewBlockedSender = "";
        NewTrustedSender = "";
    }

    private static void RemoveSender(string? sender, ObservableCollection<string> target)
    {
        if (sender is null) return;
        var match = target.FirstOrDefault(x => x.Equals(sender, StringComparison.OrdinalIgnoreCase));
        if (match is not null) target.Remove(match);
    }

    /// <summary>
    /// Flush the in-memory spam edits back to the config store. Called from the
    /// dialog's Close path (alongside App.PersistSettings). The Saved event the
    /// store raises kicks the SyncCoordinator's debounced push so block/trust
    /// lists travel to other PCs.
    /// </summary>
    public void SaveSpamConfig()
    {
        _spamConfig.Enabled = SpamEnabled;
        _spamConfig.ThresholdScore = SpamThreshold;
        _spamConfig.SubjectKeywords = SpamKeywords.ToList();
        _spamConfig.BlockedSenders = BlockedSenders.ToList();
        _spamConfig.TrustedSenders = TrustedSenders.ToList();
        // Any built-in not currently in the list counts as explicitly removed,
        // so the store's default-merge won't re-add it on the next load.
        // Re-adding it (or Restore defaults) puts it back and clears the entry.
        var present = new HashSet<string>(SpamKeywords, StringComparer.OrdinalIgnoreCase);
        _spamConfig.RemovedDefaults = SpamFilterConfig.BuiltInKeywords
            .Where(b => !present.Contains(b))
            .ToList();
        _spamStore.Save(_spamConfig);
    }

    partial void OnIsSyncBusyChanged(bool value)
    {
        SignInCommand.NotifyCanExecuteChanged();
        SignOutCommand.NotifyCanExecuteChanged();
        SyncNowCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSignedInChanged(bool value)
    {
        SignInCommand.NotifyCanExecuteChanged();
        SignOutCommand.NotifyCanExecuteChanged();
        SyncNowCommand.NotifyCanExecuteChanged();
    }

    private void ApplyLive()
    {
        // Live preview — push theme/accent through the running app immediately.
        App.Theme.Apply(_settings.Theme, _settings.Accent);
    }

    public void RemoveAccount(Account a)
    {
        var remaining = ManagedAccounts.Where(x => x.Id != a.Id).ToList();
        ManagedAccounts.Clear();
        foreach (var r in remaining) ManagedAccounts.Add(r);
        _accountStore.Save(remaining);
        try { _creds.Delete(a.Id); } catch { /* already gone, fine */ }
    }

    // --------------------------------------------------------------------- //
    // Contacts CRUD
    // --------------------------------------------------------------------- //

    public void AddContact()
    {
        var email = (NewContactEmail ?? "").Trim();
        if (string.IsNullOrEmpty(email)) return;
        var stored = _contactsStore.AddOrGet(new Contact
        {
            Id = Guid.NewGuid().ToString("N"),
            EmailAddress = email,
            DisplayName = (NewContactName ?? "").Trim(),
        });
        if (!ManagedContacts.Any(c => c.Id == stored.Id))
            ManagedContacts.Add(stored);
        NewContactName = "";
        NewContactEmail = "";
    }

    public void RemoveContact(Contact c)
    {
        var remaining = ManagedContacts.Where(x => x.Id != c.Id).ToList();
        ManagedContacts.Clear();
        foreach (var r in remaining) ManagedContacts.Add(r);
        _contactsStore.Save(remaining);
    }

    public void UpdateContact(Contact c)
    {
        c.UpdatedAt = DateTimeOffset.UtcNow;
        c.AutoCollected = false; // any explicit edit promotes the contact
        _contactsStore.Save(ManagedContacts);
    }

    // --------------------------------------------------------------------- //
    // Rules CRUD
    // --------------------------------------------------------------------- //

    public void AddOrUpdateRule(MailRule rule)
    {
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        var existing = ManagedRules.FirstOrDefault(r => r.Id == rule.Id);
        if (existing is not null)
        {
            var idx = ManagedRules.IndexOf(existing);
            ManagedRules[idx] = rule;
        }
        else
        {
            ManagedRules.Add(rule);
        }
        _rulesStore.Save(ManagedRules);
    }

    public void RemoveRule(MailRule r)
    {
        var remaining = ManagedRules.Where(x => x.Id != r.Id).ToList();
        ManagedRules.Clear();
        foreach (var x in remaining) ManagedRules.Add(x);
        _rulesStore.Save(remaining);
    }

    public void ToggleRule(MailRule r)
    {
        r.IsEnabled = !r.IsEnabled;
        r.UpdatedAt = DateTimeOffset.UtcNow;
        _rulesStore.Save(ManagedRules);
    }

    // --------------------------------------------------------------------- //
    // Sync commands
    // --------------------------------------------------------------------- //

    private void OnSessionChanged(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(() => ApplySession(_auth.Current));

    private void ApplySession(AuthSession? s)
    {
        IsSignedIn = s is not null;
        SignedInEmail = s?.Email ?? "";
        _settings.SignedInEmail = s?.Email;
        _settings.SyncEnabled = s is not null;
        _ = RefreshVaultStatusAsync();
    }

    /// <summary>
    /// Probe the Firestore vault doc to figure out which row to show in the
    /// Sync tab (Set up / Unlock / Resync). Cheap to call repeatedly — at most
    /// one HTTP GET, no KDF work.
    /// </summary>
    private async Task RefreshVaultStatusAsync()
    {
        if (!IsSignedIn)
        {
            VaultStatusText = "Sign in to enable password sync.";
            VaultIsNotSetUp = false;
            VaultIsLocked = false;
            VaultIsUnlocked = false;
            return;
        }

        VaultStatusText = "Checking…";
        var probe = await _vault.ProbeAsync();
        if (probe is Result<VaultStatus>.Fail f)
        {
            VaultStatusText = $"Vault check failed: {f.Error}";
            VaultIsNotSetUp = VaultIsLocked = VaultIsUnlocked = false;
            return;
        }
        var status = ((Result<VaultStatus>.Ok)probe).Value;
        VaultIsNotSetUp = status == VaultStatus.NotSetUp;
        VaultIsLocked   = status == VaultStatus.Locked;
        VaultIsUnlocked = status == VaultStatus.Unlocked;
        VaultStatusText = status switch
        {
            VaultStatus.NotSetUp => "Password sync isn't set up yet. Set a master passphrase to back up IMAP passwords across PCs.",
            VaultStatus.Locked   => "Password vault is locked on this PC. Enter your master passphrase to restore IMAP passwords.",
            VaultStatus.Unlocked => "Password sync is active on this PC.",
            _ => "",
        };
    }

    private async Task SetUpVaultAsync()
    {
        var dlg = new PassphraseDialog(PassphraseDialogMode.Setup) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.Passphrase)) return;

        IsVaultBusy = true;
        VaultStatusText = "Encrypting passwords and uploading…";
        try
        {
            var r = await _vault.SetupAsync(dlg.Passphrase);
            if (r is Result<bool>.Fail f)
            {
                VaultStatusText = $"Setup failed: {f.Error}";
                return;
            }
            VaultIsNotSetUp = false;
            VaultIsUnlocked = true;
            VaultStatusText = "Password sync is active on this PC.";
        }
        finally { IsVaultBusy = false; }
    }

    private async Task UnlockVaultAsync()
    {
        var dlg = new PassphraseDialog(PassphraseDialogMode.Unlock) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.Passphrase)) return;

        IsVaultBusy = true;
        VaultStatusText = "Unlocking and restoring passwords…";
        try
        {
            var r = await _vault.UnlockAsync(dlg.Passphrase);
            if (r is Result<int>.Fail f)
            {
                VaultStatusText = $"Unlock failed: {f.Error}";
                return;
            }
            var restored = ((Result<int>.Ok)r).Value;
            VaultIsLocked = false;
            VaultIsUnlocked = true;
            VaultStatusText = $"Restored {restored} password(s) to this PC's Credential Manager.";

            // Kick the sidebar so it retries IMAP folder loads with the freshly-restored passwords.
            if (Application.Current.MainWindow?.DataContext is MainViewModel mvm)
                mvm.ReloadAccounts();
        }
        finally { IsVaultBusy = false; }
    }

    private async Task SignInAsync()
    {
        IsSyncBusy = true;
        SyncStatus = "Opening browser…";
        try
        {
            var result = await _auth.SignInWithGoogleAsync();
            if (result is Result<AuthSession>.Fail f)
            {
                SyncStatus = $"Sign-in failed: {f.Error}";
                return;
            }
            // SyncCoordinator handles pull-or-seed automatically via SessionChanged.
            // Status will arrive through OnCoordinatorStateChanged.
        }
        finally { IsSyncBusy = false; }
    }

    private async Task SignOutAsync()
    {
        IsSyncBusy = true;
        try
        {
            await _auth.SignOutAsync();
            SyncStatus = "Signed out.";
        }
        finally { IsSyncBusy = false; }
    }

    private async Task SyncNowAsync()
    {
        IsSyncBusy = true;
        SyncStatus = "Pushing…";
        try { await _coordinator.PushNowAsync(); }
        finally { IsSyncBusy = false; }
    }

    private void OnCoordinatorStateChanged(object? sender, SyncEvent e)
    {
        // Coordinator events come from background tasks — marshal to UI thread.
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            // Refresh visible VM properties after a remote pull lands.
            if (e.Kind == SyncEventKind.Applied)
            {
                Theme = _settings.Theme;
                Accent = _settings.Accent;
                Density = _settings.Density;
                Ribbon = _settings.Ribbon;
                ShowHtml = _settings.ShowHtml;
                AllowRemoteImages = _settings.AllowRemoteImages;
                MarkReadDelaySeconds = _settings.MarkReadDelaySeconds;
                SyncIntervalMinutes = _settings.SyncIntervalMinutes;
                ManagedAccounts.Clear();
                foreach (var a in _accountStore.LoadAll()) ManagedAccounts.Add(a);
                ManagedContacts.Clear();
                foreach (var c in _contactsStore.LoadAll().OrderBy(x => x.DisplayName)) ManagedContacts.Add(c);
                ManagedRules.Clear();
                foreach (var r in _rulesStore.LoadAll()) ManagedRules.Add(r);
            }

            SyncStatus = e.Kind switch
            {
                SyncEventKind.Pulling => "Checking cloud…",
                SyncEventKind.Pushing => "Pushing…",
                SyncEventKind.Applied => $"Restored from cloud (saved {e.RemoteUpdatedAt?.LocalDateTime:g}).",
                SyncEventKind.Pushed  => $"Synced at {DateTime.Now:t}.",
                SyncEventKind.Failed  => $"Sync failed: {e.Detail}",
                _ => SyncStatus,
            };
        }));
    }

    // --------------------------------------------------------------------- //
    // Update commands
    // --------------------------------------------------------------------- //

    partial void OnIsUpdateBusyChanged(bool value)
    {
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        InstallUpdateCommand.NotifyCanExecuteChanged();
    }

    private async Task CheckForUpdatesAsync()
    {
        IsUpdateBusy = true;
        UpdateStatus = "Checking for updates…";
        try
        {
            var result = await _updates.CheckAsync();
            if (result is Result<UpdateInfo?>.Fail f)
            {
                UpdateStatus = $"Update check failed: {f.Error}";
                AvailableUpdate = null;
                return;
            }
            var info = ((Result<UpdateInfo?>.Ok)result).Value;
            if (info is null)
            {
                UpdateStatus = $"You're up to date ({CurrentVersionText}).";
                AvailableUpdate = null;
            }
            else
            {
                UpdateStatus = $"Update available: {info.TagName} (released {info.PublishedAt.LocalDateTime:g}).";
                AvailableUpdate = info;
            }
        }
        finally { IsUpdateBusy = false; }
    }

    private async Task InstallUpdateAsync()
    {
        var info = AvailableUpdate;
        if (info is null) return;

        IsUpdateBusy = true;
        UpdateStatus = $"Downloading {info.TagName}…";
        try
        {
            var result = await _installer.DownloadAndApplyAsync(info);
            if (result is Result<bool>.Fail f)
            {
                UpdateStatus = $"Install failed: {f.Error}";
                return;
            }
            UpdateStatus = $"Update staged. Closing now to finish install — the app will reopen automatically.";
            // Give WPF a beat to render the status before we shut down.
            await Task.Delay(800);
            Application.Current.Shutdown();
        }
        finally { IsUpdateBusy = false; }
    }
}
