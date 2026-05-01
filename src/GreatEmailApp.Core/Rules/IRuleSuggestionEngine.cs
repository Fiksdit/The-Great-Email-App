// FILE: src/GreatEmailApp.Core/Rules/IRuleSuggestionEngine.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Search;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Rules;

public sealed record RuleSuggestion(
    SenderFrequency Source,
    string SuggestedRuleName,
    string SuggestedFolderName);

public interface IRuleSuggestionEngine
{
    /// <summary>
    /// Compute frequent senders that don't already have a covering rule and
    /// haven't been dismissed by the user. Returns ordered by count, descending.
    /// </summary>
    Task<Result<List<RuleSuggestion>>> ComputeAsync(int days = 30, int minCount = 5, CancellationToken ct = default);

    /// <summary>Persist the dismissed domain so it doesn't surface again.</summary>
    void Dismiss(string domain);

    /// <summary>True if the domain is on the dismissed list.</summary>
    bool IsDismissed(string domain);
}
