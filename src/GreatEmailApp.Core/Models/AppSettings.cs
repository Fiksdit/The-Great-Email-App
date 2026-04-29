// FILE: src/GreatEmailApp.Core/Models/AppSettings.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Models;

public enum AppTheme
{
    Light,
    Dark,
    System
}

public enum RibbonStyle
{
    Simplified,
    Classic
}

public enum DensityMode
{
    Compact,
    Cozy,
    Comfortable
}

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public string Accent { get; set; } = "#3A6FF8";
    public RibbonStyle Ribbon { get; set; } = RibbonStyle.Simplified;
    public DensityMode Density { get; set; } = DensityMode.Cozy;

    // Pane widths (px)
    public double SidebarWidth { get; set; } = 264;
    public double MailListWidth { get; set; } = 380;

    public int Zoom { get; set; } = 100;

    // Sync
    public bool SyncEnabled { get; set; } = false;
    public string? SignedInEmail { get; set; }
}
