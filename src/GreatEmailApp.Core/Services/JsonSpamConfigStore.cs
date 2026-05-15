// FILE: src/GreatEmailApp.Core/Services/JsonSpamConfigStore.cs
// Created: 2026-05-15 | Revised: 2026-05-15 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Mirrors JsonRulesStore pattern: atomic write via .tmp + Move, .bad rotation
// on parse failure so the user never gets a hard crash from a corrupted file.

using System.Text.Json;
using System.Text.Json.Serialization;
using GreatEmailApp.Core.Spam;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Services;

public sealed class JsonSpamConfigStore : ISpamConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public event EventHandler? Saved;

    public SpamFilterConfig Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SpamConfigJson)) return new SpamFilterConfig();
            var json = File.ReadAllText(AppPaths.SpamConfigJson);
            if (string.IsNullOrWhiteSpace(json)) return new SpamFilterConfig();
            return JsonSerializer.Deserialize<SpamFilterConfig>(json, Options) ?? new SpamFilterConfig();
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(AppPaths.SpamConfigJson))
                    File.Move(AppPaths.SpamConfigJson, AppPaths.SpamConfigJson + ".bad", overwrite: true);
            }
            catch { }
            Console.Error.WriteLine($"[JsonSpamConfigStore.Load] {ex.Message}");
            return new SpamFilterConfig();
        }
    }

    public void Save(SpamFilterConfig config)
    {
        AppPaths.EnsureRoot();
        var json = JsonSerializer.Serialize(config, Options);
        var tmp = AppPaths.SpamConfigJson + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, AppPaths.SpamConfigJson, overwrite: true);
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
