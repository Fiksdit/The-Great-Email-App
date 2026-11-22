// FILE: src/GreatEmailApp.Core/Services/JsonRulesStore.cs
// Created: 2026-05-01 | Revised: 2026-11-21 | Rev: 2
// Changed by: Claude Opus 5.5 on behalf of James Reed

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

    /// <summary>Deletion records older than this are dropped — by then every
    /// PC has long since synced.</summary>
    private static readonly TimeSpan DeletedRetention = TimeSpan.FromDays(180);

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

        // Record deletions: ids in the old file that aren't in the new list.
        var keep = list.Select(r => r.Id).ToHashSet();
        var removed = LoadAll().Where(r => !keep.Contains(r.Id)).Select(r => r.Id).ToList();
        if (removed.Count > 0)
        {
            var deleted = LoadDeleted().ToDictionary(kv => kv.Key, kv => kv.Value);
            foreach (var id in removed) deleted[id] = DateTimeOffset.UtcNow;
            SaveDeleted(deleted);
        }

        var json = JsonSerializer.Serialize(list, Options);
        var tmp = AppPaths.RulesJson + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, AppPaths.RulesJson, overwrite: true);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyDictionary<string, DateTimeOffset> LoadDeleted()
    {
        try
        {
            if (!File.Exists(AppPaths.RulesDeletedJson)) return new Dictionary<string, DateTimeOffset>();
            return JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(
                       File.ReadAllText(AppPaths.RulesDeletedJson), Options)
                   ?? new Dictionary<string, DateTimeOffset>();
        }
        catch { return new Dictionary<string, DateTimeOffset>(); }
    }

    public void SaveDeleted(IReadOnlyDictionary<string, DateTimeOffset> deleted)
    {
        AppPaths.EnsureRoot();
        var cutoff = DateTimeOffset.UtcNow - DeletedRetention;
        var pruned = deleted.Where(kv => kv.Value > cutoff).ToDictionary(kv => kv.Key, kv => kv.Value);
        var tmp = AppPaths.RulesDeletedJson + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(pruned, Options));
        File.Move(tmp, AppPaths.RulesDeletedJson, overwrite: true);
    }
}
