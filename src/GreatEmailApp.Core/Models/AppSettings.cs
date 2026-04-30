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

    // Reading
    /// <summary>Render HTML email bodies (Phase 5 plumbs the WebView2 surface).
    /// Until then this gates whether HTML or plain-text body is preferred.</summary>
    public bool ShowHtml { get; set; } = true;
    /// <summary>Wait this many seconds before marking a message as read after
    /// it's selected. 0 = mark immediately; -1 = never auto-mark.</summary>
    public int MarkReadDelaySeconds { get; set; } = 2;

    // Send / Receive
    /// <summary>Auto Send/Receive interval in minutes. 0 = manual only.
    /// (Auto-sync timer lands alongside IMAP IDLE in Phase 5.)</summary>
    public int SyncIntervalMinutes { get; set; } = 5;

    // Sync (Firebase — Phase 4)
    public bool SyncEnabled { get; set; } = false;
    public string? SignedInEmail { get; set; }

    // First-run UX: once the sign-in/skip screen has been shown, don't show it again.
    public bool HasShownFirstRun { get; set; } = false;
}
