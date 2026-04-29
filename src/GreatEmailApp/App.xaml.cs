// FILE: src/GreatEmailApp/App.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Services;

namespace GreatEmailApp;

public partial class App : Application
{
    public static ThemeManager Theme { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // NOTE: settings persistence lands in Phase 3; for Phase 1 we boot with defaults
        // matching the design (dark theme, default accent).
        Theme.Apply(AppTheme.Dark, "#3A6FF8");
    }
}
