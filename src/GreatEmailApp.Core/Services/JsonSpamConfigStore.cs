// FILE: src/GreatEmailApp.Core/Services/JsonSpamConfigStore.cs
// Created: 2026-05-15 | Revised: 2026-05-15 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Mirrors JsonRulesStore pattern: atomic write via .tmp + Move, .bad rotation
// on parse failure so the user never gets a hard crash from a corrupted file.
//
// On load, merges in any built-in default keywords missing from the user's
// file so a shipped keyword-list expansion reaches existing installs without
// requiring them to delete spam-filter.json. The trade-off: in Phase 1 the
// user can't permanently REMOVE a built-in keyword by editing the file (it
// would be re-merged on next load). Phase 2 will add a "RemovedDefaults"
// list so explicit removals stick.

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
        SpamFilterConfig config;
        try
        {
            if (!File.Exists(AppPaths.SpamConfigJson))
            {
                config = new SpamFilterConfig();
            }
            else
            {
                var json = File.ReadAllText(AppPaths.SpamConfigJson);
                if (string.IsNullOrWhiteSpace(json))
                {
                    config = new SpamFilterConfig();
                }
                else
                {
                    config = JsonSerializer.Deserialize<SpamFilterConfig>(json, Options) ?? new SpamFilterConfig();
                }
            }
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
            config = new SpamFilterConfig();
        }

        // Merge in any built-in keywords missing from the loaded file. Lets a
        // shipping default expansion reach existing installs. Comparison is
        // case-insensitive; user-customized variations of casing are preserved.
        var existing = new HashSet<string>(config.SubjectKeywords, StringComparer.OrdinalIgnoreCase);
        foreach (var builtin in SpamFilterConfig.BuiltInKeywords)
        {
            if (existing.Add(builtin))
                config.SubjectKeywords.Add(builtin);
        }

        return config;
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
