// FILE: src/GreatEmailApp/ViewModels/SettingsViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 4
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreatEmailApp.Core.Auth;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;
using GreatEmailApp.Core.Sync;
using GreatEmailApp.Core.Updates;

namespace GreatEmailApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IAccountStore _accountStore;
    private readonly IContactsStore _contactsStore;
    private readonly IRulesStore _rulesStore;
    private readonly ICredentialStore _creds;
    private readonly ISettingsStore _settingsStore;
    private readonly IAuthService _auth;
    private readonly IFirestoreSyncService _sync;
    private readonly SyncCoordinator _coordinator;
    private readonly IUpdateService _updates;
    private readonly IUpdateInstaller _installer;

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

    public IAsyncRelayCommand SignInCommand { get; }
    public IAsyncRelayCommand SignOutCommand { get; }
    public IAsyncRelayCommand SyncNowCommand { get; }
    public IAsyncRelayCommand CheckForUpdatesCommand { get; }
    public IAsyncRelayCommand InstallUpdateCommand { get; }

    public SettingsViewModel(
        AppSettings settings,
        IAccountStore accountStore,
        IContactsStore contactsStore,
        IRulesStore rulesStore,
        ICredentialStore creds,
        ISettingsStore settingsStore,
        IAuthService auth,
        IFirestoreSyncService sync,
        SyncCoordinator coordinator,
        IUpdateService updates,
        IUpdateInstaller installer)
    {
        _settings = settings;
        _accountStore = accountStore;
        _contactsStore = contactsStore;
        _rulesStore = rulesStore;
        _creds = creds;
        _settingsStore = settingsStore;
        _auth = auth;
        _sync = sync;
        _coordinator = coordinator;
        _updates = updates;
        _installer = installer;
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

        SignInCommand           = new AsyncRelayCommand(SignInAsync,         () => !IsSyncBusy && !IsSignedIn);
        SignOutCommand          = new AsyncRelayCommand(SignOutAsync,        () => !IsSyncBusy &&  IsSignedIn);
        SyncNowCommand          = new AsyncRelayCommand(SyncNowAsync,        () => !IsSyncBusy &&  IsSignedIn);
        CheckForUpdatesCommand  = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsUpdateBusy);
        InstallUpdateCommand    = new AsyncRelayCommand(InstallUpdateAsync,   () => !IsUpdateBusy && AvailableUpdate is not null);

        _auth.SessionChanged += OnSessionChanged;
        ApplySession(_auth.Current);
    }

    partial void OnThemeChanged(AppTheme value)   { _settings.Theme = value;   ApplyLive(); }
    partial void OnAccentChanged(string value)    { _settings.Accent = value;  ApplyLive(); }
    partial void OnDensityChanged(DensityMode v)  => _settings.Density = v;
    partial void OnRibbonChanged(RibbonStyle v)   => _settings.Ribbon = v;
    partial void OnShowHtmlChanged(bool v)        => _settings.ShowHtml = v;
    partial void OnAllowRemoteImagesChanged(bool v) => _settings.AllowRemoteImages = v;
    partial void OnEnableNewMailNotificationsChanged(bool v) => _settings.EnableNewMailNotifications = v;
    partial void OnMarkReadDelaySecondsChanged(int v) => _settings.MarkReadDelaySeconds = v;
    partial void OnSyncIntervalMinutesChanged(int v)  => _settings.SyncIntervalMinutes = v;

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
