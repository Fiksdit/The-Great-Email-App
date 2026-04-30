// FILE: src/GreatEmailApp/ViewModels/SettingsViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 2
// Changed by: Claude Sonnet 4.6 on behalf of James Reed

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreatEmailApp.Core.Config;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings          _settings;
    private readonly IAccountStore        _accountStore;
    private readonly ICredentialStore     _creds;
    private readonly IFirebaseAuthService _auth;
    private readonly IFirestoreSyncService _sync;

    [ObservableProperty] private string activeTab = "Appearance";

    // Appearance
    [ObservableProperty] private AppTheme    theme;
    [ObservableProperty] private string      accent = "#3A6FF8";
    [ObservableProperty] private DensityMode density;
    [ObservableProperty] private RibbonStyle ribbon;

    // General
    [ObservableProperty] private bool showHtml;
    [ObservableProperty] private int  markReadDelaySeconds;
    [ObservableProperty] private int  syncIntervalMinutes;

    // Sync tab state
    [ObservableProperty] private bool   isSignedIn;
    [ObservableProperty] private string signedInEmail  = "";
    [ObservableProperty] private bool   isSyncing;
    [ObservableProperty] private string syncStatus     = "";

    public string[] Accents { get; } =
    {
        "#3A6FF8", "#14a37f", "#8a5cf5", "#0ea5e9", "#d29014", "#d4406b",
    };

    public ObservableCollection<Account> ManagedAccounts { get; } = new();

    public bool FirebaseIsConfigured => FirebaseConfig.IsConfigured;

    public SettingsViewModel(
        AppSettings settings,
        IAccountStore accountStore,
        ICredentialStore creds,
        IFirebaseAuthService auth,
        IFirestoreSyncService sync)
    {
        _settings     = settings;
        _accountStore = accountStore;
        _creds        = creds;
        _auth         = auth;
        _sync         = sync;

        // Snapshot from settings
        theme                = settings.Theme;
        accent               = settings.Accent;
        density              = settings.Density;
        ribbon               = settings.Ribbon;
        showHtml             = settings.ShowHtml;
        markReadDelaySeconds = settings.MarkReadDelaySeconds;
        syncIntervalMinutes  = settings.SyncIntervalMinutes;

        // Sync state
        isSignedIn    = auth.CurrentUser is not null;
        signedInEmail = auth.CurrentUser?.Email ?? "";

        foreach (var a in _accountStore.LoadAll())
            ManagedAccounts.Add(a);

        _auth.UserChanged += OnUserChanged;
    }

    private void OnUserChanged(object? sender, FirebaseUser? user)
    {
        IsSignedIn    = user is not null;
        SignedInEmail = user?.Email ?? "";
    }

    // ── Appearance ─────────────────────────────────────────────────────────────

    partial void OnThemeChanged(AppTheme value)    { _settings.Theme   = value; ApplyLive(); }
    partial void OnAccentChanged(string value)     { _settings.Accent  = value; ApplyLive(); }
    partial void OnDensityChanged(DensityMode value) => _settings.Density = value;
    partial void OnRibbonChanged(RibbonStyle value)  => _settings.Ribbon  = value;
    partial void OnShowHtmlChanged(bool value)        => _settings.ShowHtml = value;
    partial void OnMarkReadDelaySecondsChanged(int v) => _settings.MarkReadDelaySeconds = v;
    partial void OnSyncIntervalMinutesChanged(int v)  => _settings.SyncIntervalMinutes  = v;

    private void ApplyLive() => App.Theme.Apply(_settings.Theme, _settings.Accent);

    // ── Accounts ───────────────────────────────────────────────────────────────

    public void RemoveAccount(Account a)
    {
        var remaining = ManagedAccounts.Where(x => x.Id != a.Id).ToList();
        ManagedAccounts.Clear();
        foreach (var r in remaining) ManagedAccounts.Add(r);
        _accountStore.Save(remaining);
        try { _creds.Delete(a.Id); } catch { /* already gone */ }
    }

    // ── Sync commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (!FirebaseConfig.IsConfigured)
        {
            SyncStatus = "Firebase is not configured. Fill in FirebaseConfig.cs.";
            return;
        }

        IsSyncing  = true;
        SyncStatus = "Opening browser…";
        try
        {
            var user = await _auth.SignInWithGoogleAsync();
            if (user is not null)
            {
                _settings.SyncEnabled   = true;
                _settings.SignedInEmail = user.Email;
                SyncStatus = $"Signed in as {user.Email}";
                _ = _sync.PushAsync(_settings, _accountStore.LoadAll());
            }
        }
        catch (OperationCanceledException)
        {
            SyncStatus = "Sign-in cancelled.";
        }
        catch (Exception ex)
        {
            SyncStatus = $"Sign-in failed: {ex.Message}";
        }
        finally { IsSyncing = false; }
    }

    [RelayCommand]
    private void SignOut()
    {
        _auth.SignOut();
        _settings.SyncEnabled   = false;
        _settings.SignedInEmail = null;
        SyncStatus = "Signed out.";
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (_auth.CurrentUser is null) return;

        IsSyncing  = true;
        SyncStatus = "Syncing…";
        try
        {
            // Push first (local changes win for current session).
            await _sync.PushAsync(_settings, _accountStore.LoadAll());
            // Then pull to pick up changes from other devices.
            await App.PullAndApplyAsync();
            SyncStatus = $"Last synced {DateTime.Now:t}";
        }
        catch (Exception ex)
        {
            SyncStatus = $"Sync failed: {ex.Message}";
        }
        finally { IsSyncing = false; }
    }
}
