// FILE: src/GreatEmailApp/ViewModels/SettingsViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IAccountStore _accountStore;
    private readonly ICredentialStore _creds;

    [ObservableProperty] private string activeTab = "Appearance";

    [ObservableProperty] private AppTheme theme;
    [ObservableProperty] private string accent = "#3A6FF8";
    [ObservableProperty] private DensityMode density;
    [ObservableProperty] private RibbonStyle ribbon;

    [ObservableProperty] private bool showHtml;
    [ObservableProperty] private int markReadDelaySeconds;
    [ObservableProperty] private int syncIntervalMinutes;

    public string[] Accents { get; } = new[]
    {
        "#3A6FF8", "#14a37f", "#8a5cf5", "#0ea5e9", "#d29014", "#d4406b",
    };

    public ObservableCollection<Account> ManagedAccounts { get; } = new();

    public SettingsViewModel(AppSettings settings, IAccountStore accountStore, ICredentialStore creds)
    {
        _settings = settings;
        _accountStore = accountStore;
        _creds = creds;

        // Snapshot from settings — mutations are propagated to live App.Settings
        // immediately so theme changes preview in real time.
        theme = settings.Theme;
        accent = settings.Accent;
        density = settings.Density;
        ribbon = settings.Ribbon;
        showHtml = settings.ShowHtml;
        markReadDelaySeconds = settings.MarkReadDelaySeconds;
        syncIntervalMinutes = settings.SyncIntervalMinutes;

        foreach (var a in _accountStore.LoadAll())
            ManagedAccounts.Add(a);
    }

    partial void OnThemeChanged(AppTheme value)
    {
        _settings.Theme = value;
        ApplyLive();
    }

    partial void OnAccentChanged(string value)
    {
        _settings.Accent = value;
        ApplyLive();
    }

    partial void OnDensityChanged(DensityMode value) => _settings.Density = value;
    partial void OnRibbonChanged(RibbonStyle value) => _settings.Ribbon = value;
    partial void OnShowHtmlChanged(bool value) => _settings.ShowHtml = value;
    partial void OnMarkReadDelaySecondsChanged(int value) => _settings.MarkReadDelaySeconds = value;
    partial void OnSyncIntervalMinutesChanged(int value) => _settings.SyncIntervalMinutes = value;

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
}
