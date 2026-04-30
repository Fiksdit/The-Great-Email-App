// FILE: src/GreatEmailApp.Core/Storage/AppPaths.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Storage;

/// <summary>
/// Centralized paths for app-local data. Everything lives under
/// %LOCALAPPDATA%\GreatEmailApp\ so it's per-user, not roamed.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GreatEmailApp");

    public static string AccountsJson => Path.Combine(Root, "accounts.json");
    public static string SettingsJson => Path.Combine(Root, "settings.json");
    public static string CacheDb     => Path.Combine(Root, "cache.db");
    public static string LogsFolder  => Path.Combine(Root, "logs");

    /// <summary>Ensures the root folder exists. Safe to call repeatedly.</summary>
    public static void EnsureRoot()
    {
        if (!Directory.Exists(Root))
            Directory.CreateDirectory(Root);
    }
}
