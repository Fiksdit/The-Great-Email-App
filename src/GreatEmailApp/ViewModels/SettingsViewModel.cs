// FILE: src/GreatEmailApp/ViewModels/SettingsViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 3
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
    private readonly ICredentialStore _creds;
    private readonly ISettingsStore _settingsStore;
    private readonly IAuthService _auth;
    private readonly IFirestoreSyncService _sync;
    private readonly IUpdateService _updates;
    private readonly IUpdateInstaller _installer;

    [ObservableProperty] private string activeTab = "Appearance";

    [ObservableProperty] private AppTheme theme;
    [ObservableProperty] private string accent = "#3A6FF8";
    [ObservableProperty] private DensityMode density;
    [ObservableProperty] private RibbonStyle ribbon;

    [ObservableProperty] private bool showHtml;
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

    public IAsyncRelayCommand SignInCommand { get; }
    public IAsyncRelayCommand SignOutCommand { get; }
    public IAsyncRelayCommand SyncNowCommand { get; }
    public IAsyncRelayCommand CheckForUpdatesCommand { get; }
    public IAsyncRelayCommand InstallUpdateCommand { get; }

    public SettingsViewModel(
        AppSettings settings,
        IAccountStore accountStore,
        ICredentialStore creds,
        ISettingsStore settingsStore,
        IAuthService auth,
        IFirestoreSyncService sync,
        IUpdateService updates,
        IUpdateInstaller installer)
    {
        _settings = settings;
        _accountStore = accountStore;
        _creds = creds;
        _settingsStore = settingsStore;
        _auth = auth;
        _sync = sync;
        _updates = updates;
        _installer = installer;

        theme = settings.Theme;
        accent = settings.Accent;
        density = settings.Density;
        ribbon = settings.Ribbon;
        showHtml = settings.ShowHtml;
        markReadDelaySeconds = settings.MarkReadDelaySeconds;
        syncIntervalMinutes = settings.SyncIntervalMinutes;

        foreach (var a in _accountStore.LoadAll())
            ManagedAccounts.Add(a);

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

            // First sign-in on this PC: pull anything the user has on another device.
            // No remote? Push current local so this device seeds the cloud.
            SyncStatus = "Syncing…";
            await PullOrSeedAsync();
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
        try
        {
            // Manual sync = push local. Pull-on-sign-in covers the new-device case;
            // multi-device merge with auto-pull is deferred (rulebook decision log:
            // last-write-wins is sufficient for v1).
            _settingsStore.Save(_settings);
            var snapshot = new SyncSnapshot(
                _settings,
                _accountStore.LoadAll().ToList(),
                DateTimeOffset.UtcNow);
            var result = await _sync.PushAsync(snapshot);
            SyncStatus = result is Result<bool>.Ok
                ? $"Synced at {DateTime.Now:t}."
                : $"Sync failed: {((Result<bool>.Fail)result).Error}";
        }
        finally { IsSyncBusy = false; }
    }

    private async Task PullOrSeedAsync()
    {
        var pull = await _sync.PullAsync();
        if (pull is Result<SyncSnapshot?>.Fail pf)
        {
            SyncStatus = $"Pull failed: {pf.Error}";
            return;
        }
        var remote = ((Result<SyncSnapshot?>.Ok)pull).Value;

        if (remote is null)
        {
            // First device on this Google account — seed the cloud.
            _settingsStore.Save(_settings);
            var snapshot = new SyncSnapshot(
                _settings,
                _accountStore.LoadAll().ToList(),
                DateTimeOffset.UtcNow);
            var push = await _sync.PushAsync(snapshot);
            SyncStatus = push is Result<bool>.Ok
                ? "Signed in. No prior backup — uploaded current settings."
                : $"Signed in, but upload failed: {((Result<bool>.Fail)push).Error}";
            return;
        }

        // Apply remote → local.
        ApplyRemote(remote);
        SyncStatus = $"Signed in. Restored from cloud (saved {remote.UpdatedAt.LocalDateTime:g}). Restart to see all changes.";
    }

    private void ApplyRemote(SyncSnapshot remote)
    {
        // Settings — copy field-by-field so the live AppSettings instance the rest
        // of the app holds gets updated, not replaced.
        _settings.Theme = remote.Settings.Theme;
        _settings.Accent = remote.Settings.Accent;
        _settings.Ribbon = remote.Settings.Ribbon;
        _settings.Density = remote.Settings.Density;
        _settings.SidebarWidth = remote.Settings.SidebarWidth;
        _settings.MailListWidth = remote.Settings.MailListWidth;
        _settings.Zoom = remote.Settings.Zoom;
        _settings.ShowHtml = remote.Settings.ShowHtml;
        _settings.MarkReadDelaySeconds = remote.Settings.MarkReadDelaySeconds;
        _settings.SyncIntervalMinutes = remote.Settings.SyncIntervalMinutes;
        _settingsStore.Save(_settings);

        // Mirror visible VM properties so the dialog reflects pulled values.
        Theme = _settings.Theme;
        Accent = _settings.Accent;
        Density = _settings.Density;
        Ribbon = _settings.Ribbon;
        ShowHtml = _settings.ShowHtml;
        MarkReadDelaySeconds = _settings.MarkReadDelaySeconds;
        SyncIntervalMinutes = _settings.SyncIntervalMinutes;
        ApplyLive();

        // Accounts — overwrite local roster. Existing Credential Manager entries
        // for accounts that match by Id stay valid; new accounts will need a
        // password re-entry on first connect (rulebook §7B).
        _accountStore.Save(remote.Accounts);
        ManagedAccounts.Clear();
        foreach (var a in remote.Accounts) ManagedAccounts.Add(a);
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
