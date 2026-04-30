// FILE: src/GreatEmailApp.Core/Services/JsonSettingsStore.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Text.Json;
using System.Text.Json.Serialization;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Services;

/// <summary>
/// Persists AppSettings to %LOCALAPPDATA%\GreatEmailApp\settings.json.
/// Atomic write via .tmp + rename. Corrupt files are sidelined as .bad
/// and we fall back to defaults — startup never fails on settings.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsJson)) return new AppSettings();
            var json = File.ReadAllText(AppPaths.SettingsJson);
            if (string.IsNullOrWhiteSpace(json)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(AppPaths.SettingsJson))
                    File.Move(AppPaths.SettingsJson, AppPaths.SettingsJson + ".bad", overwrite: true);
            }
            catch { }
            Console.Error.WriteLine($"[JsonSettingsStore.Load] {ex.Message}");
            return new AppSettings();
        }
    }

    public event EventHandler? Saved;

    public void Save(AppSettings settings)
    {
        AppPaths.EnsureRoot();
        var json = JsonSerializer.Serialize(settings, Options);
        var tmp = AppPaths.SettingsJson + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, AppPaths.SettingsJson, overwrite: true);
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
