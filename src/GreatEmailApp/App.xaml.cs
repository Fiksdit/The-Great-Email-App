// FILE: src/GreatEmailApp/App.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 4
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.IO;
using System.Windows;
using GreatEmailApp.Core.Config;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;
using GreatEmailApp.Core.Storage;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureRoot();

        Config = AppConfig.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Imap = new ImapService();
        Credentials = new WindowsCredentialStore();
        Accounts = new JsonAccountStore();
        SettingsStore = new JsonSettingsStore();
        Settings = SettingsStore.Load();

        Theme.Apply(Settings.Theme, Settings.Accent);
    }

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
