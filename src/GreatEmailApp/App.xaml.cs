// FILE: src/GreatEmailApp/App.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;
using GreatEmailApp.Core.Storage;
using GreatEmailApp.Services;

namespace GreatEmailApp;

public partial class App : Application
{
    public static ThemeManager Theme { get; } = new();

    // Lightweight service locator — Phase 2 doesn't need a full DI container.
    // Constructed once at startup; replaced by DI when complexity warrants it.
    public static IImapService Imap { get; private set; } = null!;
    public static ICredentialStore Credentials { get; private set; } = null!;
    public static IAccountStore Accounts { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureRoot();

        Imap = new ImapService();
        Credentials = new WindowsCredentialStore();
        Accounts = new JsonAccountStore();

        // NOTE: settings persistence lands in Phase 3; for now boot with defaults
        // matching the design (dark theme, default accent).
        Theme.Apply(AppTheme.Dark, "#3A6FF8");
    }
}
