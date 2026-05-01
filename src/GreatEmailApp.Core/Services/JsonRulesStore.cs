// FILE: src/GreatEmailApp.Core/Services/JsonRulesStore.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Text.Json;
using System.Text.Json.Serialization;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Services;

public sealed class JsonRulesStore : IRulesStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public event EventHandler? Saved;

    public IReadOnlyList<MailRule> LoadAll()
    {
        try
        {
            if (!File.Exists(AppPaths.RulesJson)) return Array.Empty<MailRule>();
            var json = File.ReadAllText(AppPaths.RulesJson);
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<MailRule>();
            return JsonSerializer.Deserialize<List<MailRule>>(json, Options) ?? new List<MailRule>();
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(AppPaths.RulesJson))
                    File.Move(AppPaths.RulesJson, AppPaths.RulesJson + ".bad", overwrite: true);
            }
            catch { }
            Console.Error.WriteLine($"[JsonRulesStore.LoadAll] {ex.Message}");
            return Array.Empty<MailRule>();
        }
    }

    public void Save(IEnumerable<MailRule> rules)
    {
        AppPaths.EnsureRoot();
        var list = rules.ToList();
        var json = JsonSerializer.Serialize(list, Options);
        var tmp = AppPaths.RulesJson + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, AppPaths.RulesJson, overwrite: true);
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
