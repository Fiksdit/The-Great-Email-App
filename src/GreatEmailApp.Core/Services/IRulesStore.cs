// FILE: src/GreatEmailApp.Core/Services/IRulesStore.cs
// Created: 2026-05-01 | Revised: 2026-11-21 | Rev: 2
// Changed by: Claude Opus 5.5 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

public interface IRulesStore
{
    IReadOnlyList<MailRule> LoadAll();

    /// <summary>Saves the list. Any rule id that was in the previous list and
    /// isn't in this one is recorded as deleted (see <see cref="LoadDeleted"/>),
    /// so sync can tell "deleted here" apart from "never seen here".</summary>
    void Save(IEnumerable<MailRule> rules);

    /// <summary>Rule id → when it was deleted (FIX-2026-11-21-004). Synced so a
    /// deletion on one PC isn't undone by the merge on another.</summary>
    IReadOnlyDictionary<string, DateTimeOffset> LoadDeleted();
    void SaveDeleted(IReadOnlyDictionary<string, DateTimeOffset> deleted);

    event EventHandler? Saved;
}
