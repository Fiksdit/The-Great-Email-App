// FILE: src/GreatEmailApp.Core/Services/FolderCache.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Per-account folder-tree cache so the sidebar shows folders instantly on
// app start. The live IMAP LIST still runs in the background and replaces
// the cached set if anything changed — this is purely a paint-faster trick.
//
// Storage: a single JSON dict at %LOCALAPPDATA%\GreatEmailApp\folders-cache.json,
// accountId → List<Folder>. Local-only, never synced (folder hierarchy is
// per-server reality, not user state).

using System.Text.Json;
using System.Text.Json.Serialization;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Services;

public interface IFolderCache
{
    IReadOnlyList<Folder> Load(string accountId);
    void Save(string accountId, IEnumerable<Folder> folders);
}

public sealed class JsonFolderCache : IFolderCache
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();

    public IReadOnlyList<Folder> Load(string accountId)
    {
        try
        {
            lock (_gate)
            {
                var dict = LoadAll();
                return dict.TryGetValue(accountId, out var list) ? list : Array.Empty<Folder>();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[JsonFolderCache.Load] {ex.Message}");
            return Array.Empty<Folder>();
        }
    }

    public void Save(string accountId, IEnumerable<Folder> folders)
    {
        try
        {
            AppPaths.EnsureRoot();
            lock (_gate)
            {
                var dict = LoadAll();
                dict[accountId] = folders.ToList();
                var json = JsonSerializer.Serialize(dict, Options);
                var tmp = AppPaths.FoldersCacheJson + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, AppPaths.FoldersCacheJson, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[JsonFolderCache.Save] {ex.Message}");
        }
    }

    private static Dictionary<string, List<Folder>> LoadAll()
    {
        if (!File.Exists(AppPaths.FoldersCacheJson)) return new();
        var json = File.ReadAllText(AppPaths.FoldersCacheJson);
        if (string.IsNullOrWhiteSpace(json)) return new();
        return JsonSerializer.Deserialize<Dictionary<string, List<Folder>>>(json, Options) ?? new();
    }
}
