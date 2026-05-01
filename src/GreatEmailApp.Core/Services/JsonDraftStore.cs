// FILE: src/GreatEmailApp.Core/Services/JsonDraftStore.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Drafts are local-only — they ride the same atomic-write pattern as the
// other JSON stores. NOT synced to Firestore: half-typed messages on one PC
// shouldn't pop up on the other (and Firestore document size limits make
// this awkward anyway). IMAP Drafts append is a future iteration.

using System.Text.Json;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Services;

public sealed class JsonDraftStore : IDraftStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly object _gate = new();

    public event EventHandler? Changed;

    public IReadOnlyList<Draft> LoadAll()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(AppPaths.DraftsJson)) return Array.Empty<Draft>();
                var json = File.ReadAllText(AppPaths.DraftsJson);
                if (string.IsNullOrWhiteSpace(json)) return Array.Empty<Draft>();
                return JsonSerializer.Deserialize<List<Draft>>(json, Options) ?? new List<Draft>();
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(AppPaths.DraftsJson))
                        File.Move(AppPaths.DraftsJson, AppPaths.DraftsJson + ".bad", overwrite: true);
                }
                catch { }
                Console.Error.WriteLine($"[JsonDraftStore.LoadAll] {ex.Message}");
                return Array.Empty<Draft>();
            }
        }
    }

    public void Save(Draft draft)
    {
        draft.UpdatedAt = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            var list = LoadAll().ToList();
            var idx = list.FindIndex(d => d.Id == draft.Id);
            if (idx >= 0) list[idx] = draft;
            else list.Add(draft);
            WriteAll(list);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Delete(string id)
    {
        lock (_gate)
        {
            var list = LoadAll().Where(d => d.Id != id).ToList();
            WriteAll(list);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void WriteAll(IEnumerable<Draft> list)
    {
        AppPaths.EnsureRoot();
        var json = JsonSerializer.Serialize(list, Options);
        var tmp = AppPaths.DraftsJson + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, AppPaths.DraftsJson, overwrite: true);
    }
}
