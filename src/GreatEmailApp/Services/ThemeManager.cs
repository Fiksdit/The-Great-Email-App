// FILE: src/GreatEmailApp/Services/ThemeManager.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System;
using System.Windows;
using System.Windows.Media;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Services;

/// <summary>
/// Swaps the theme dictionary at runtime and overlays an accent color.
/// The brush keys (AccentBrush, PaneBackgroundBrush, etc.) stay stable;
/// only their values change. All consumers must use {DynamicResource ...}.
/// </summary>
public sealed class ThemeManager
{
    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
    public string CurrentAccent { get; private set; } = "#3A6FF8";

    public event EventHandler? ThemeChanged;

    public void Apply(AppTheme theme, string accentHex)
    {
        var resolved = theme;
        if (theme == AppTheme.System)
        {
            resolved = IsSystemDark() ? AppTheme.Dark : AppTheme.Light;
        }

        var dictUri = resolved == AppTheme.Dark
            ? new Uri("Themes/Dark.xaml", UriKind.Relative)
            : new Uri("Themes/Light.xaml", UriKind.Relative);

        var newDict = new ResourceDictionary { Source = dictUri };
        var merged = Application.Current.Resources.MergedDictionaries;

        // Remove any existing theme dictionary (Dark or Light).
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source?.OriginalString ?? "";
            if (src.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase) ||
                src.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(i);
            }
        }
        // Insert after Tokens.xaml (index 0) so Controls.xaml still merges last.
        int insertAt = 1;
        if (merged.Count < 1) insertAt = merged.Count;
        merged.Insert(insertAt, newDict);

        CurrentTheme = theme;
        CurrentAccent = accentHex;
        ApplyAccent(accentHex);

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetAccent(string accentHex)
    {
        CurrentAccent = accentHex;
        ApplyAccent(accentHex);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyAccent(string hex)
    {
        var c = Parse(hex);
        var hover = Lighten(c, 0.12);
        var pressed = Darken(c, 0.12);
        var soft = Color.FromArgb((byte)(0.18 * 255), c.R, c.G, c.B);
        var softer = Color.FromArgb((byte)(0.10 * 255), c.R, c.G, c.B);

        Application.Current.Resources["AccentBrush"] = new SolidColorBrush(c);
        Application.Current.Resources["AccentHoverBrush"] = new SolidColorBrush(hover);
        Application.Current.Resources["AccentPressedBrush"] = new SolidColorBrush(pressed);
        Application.Current.Resources["AccentSoftBrush"] = new SolidColorBrush(soft);
        Application.Current.Resources["AccentSofterBrush"] = new SolidColorBrush(softer);
    }

    private static Color Parse(string hex)
    {
        var s = hex.TrimStart('#');
        if (s.Length == 3) s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        return Color.FromRgb(
            Convert.ToByte(s.Substring(0, 2), 16),
            Convert.ToByte(s.Substring(2, 2), 16),
            Convert.ToByte(s.Substring(4, 2), 16));
    }

    private static Color Lighten(Color c, double amt)
    {
        byte F(byte x) => (byte)Math.Round(x + (255 - x) * amt);
        return Color.FromRgb(F(c.R), F(c.G), F(c.B));
    }

    private static Color Darken(Color c, double amt)
    {
        byte F(byte x) => (byte)Math.Round(x * (1 - amt));
        return Color.FromRgb(F(c.R), F(c.G), F(c.B));
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int i) return i == 0;
        }
        catch { }
        return true; // default to dark on failure
    }
}
