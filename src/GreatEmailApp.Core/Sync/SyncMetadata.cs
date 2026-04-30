// FILE: src/GreatEmailApp.Core/Sync/SyncMetadata.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Text.Json;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Sync;

/// <summary>
/// Per-machine sync bookkeeping. Lives at <see cref="AppPaths.SyncMetaJson"/>
/// — local-only, not synced. The whole point of this file is to be the
/// machine-local truth that <i>can't</i> be overwritten by a remote pull.
/// </summary>
public sealed class SyncMetadata
{
    /// <summary>Timestamp written into the snapshot the last time we pushed
    /// (or the timestamp on a remote snapshot we successfully applied).</summary>
    public DateTimeOffset? LastSyncedAt { get; set; }

    public static SyncMetadata Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SyncMetaJson)) return new();
            var json = File.ReadAllText(AppPaths.SyncMetaJson);
            if (string.IsNullOrWhiteSpace(json)) return new();
            return JsonSerializer.Deserialize<SyncMetadata>(json) ?? new();
        }
        catch { return new(); }
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureRoot();
            var json = JsonSerializer.Serialize(this);
            var tmp = AppPaths.SyncMetaJson + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, AppPaths.SyncMetaJson, overwrite: true);
        }
        catch { /* sync metadata is best-effort */ }
    }

    /// <summary>
    /// True if either the settings.json or accounts.json file has been written
    /// AFTER the last successful sync — meaning we have local edits the cloud
    /// hasn't seen yet, and a pull would lose them.
    /// </summary>
    public bool HasUnpushedLocalChanges()
    {
        if (LastSyncedAt is null) return AnyLocalDataPresent(); // never synced + local data → push
        var threshold = LastSyncedAt.Value.UtcDateTime;
        var newest = NewestLocalDataMtime();
        if (newest is null) return false;
        // 2-second slop absorbs filesystem timestamp granularity differences.
        return newest.Value > threshold.AddSeconds(-2);
    }

    private static DateTime? NewestLocalDataMtime()
    {
        DateTime? newest = null;
        foreach (var p in new[] { AppPaths.AccountsJson, AppPaths.SettingsJson })
        {
            if (!File.Exists(p)) continue;
            var t = File.GetLastWriteTimeUtc(p);
            if (newest is null || t > newest) newest = t;
        }
        return newest;
    }

    private static bool AnyLocalDataPresent()
    {
        // Accounts being non-empty is the meaningful signal — settings always exist
        // (we write defaults). If settings.json is the only file, the cloud should
        // overwrite freely.
        if (!File.Exists(AppPaths.AccountsJson)) return false;
        var json = File.ReadAllText(AppPaths.AccountsJson).Trim();
        return json.Length > 2; // anything beyond "[]" means real accounts
    }
}
