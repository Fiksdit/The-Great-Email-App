// FILE: src/GreatEmailApp.Core/Rules/RuleSuggestionEngine.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Suggests "you've gotten N emails from X — want a rule?" by querying the
// SQLite cache for sender-domain frequencies, then filtering out anything
// already covered by an existing rule or dismissed by the user.
//
// "Already covered" is conservative — if any existing rule has a From
// condition whose value contains the domain, we skip suggesting it. False
// negatives (we miss a suggestion) are fine; false positives (we nag about
// something the user already handled) feel buggy.

using System.Text.Json;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Search;
using GreatEmailApp.Core.Services;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Rules;

public sealed class RuleSuggestionEngine : IRuleSuggestionEngine
{
    private readonly IMessageCache _cache;
    private readonly IRulesStore _rules;
    private readonly object _gate = new();
    private HashSet<string>? _dismissedCache;

    public RuleSuggestionEngine(IMessageCache cache, IRulesStore rules)
    {
        _cache = cache;
        _rules = rules;
    }

    public async Task<Result<List<RuleSuggestion>>> ComputeAsync(int days = 30, int minCount = 5, CancellationToken ct = default)
    {
        var freqResult = await _cache.GetSenderFrequenciesAsync(days, minCount, ct).ConfigureAwait(false);
        if (freqResult is Result<List<SenderFrequency>>.Fail f)
            return Result.Fail<List<RuleSuggestion>>(f.Error);
        var freqs = ((Result<List<SenderFrequency>>.Ok)freqResult).Value;

        var dismissed = LoadDismissed();
        var rules = _rules.LoadAll();
        var coveredDomains = ExtractCoveredDomains(rules);

        var suggestions = new List<RuleSuggestion>();
        foreach (var freq in freqs)
        {
            if (dismissed.Contains(freq.Domain)) continue;
            if (coveredDomains.Any(d => freq.Domain.Contains(d, StringComparison.OrdinalIgnoreCase))) continue;

            var domainRoot = freq.Domain.Split('.').FirstOrDefault() ?? freq.Domain;
            var titled = string.IsNullOrEmpty(domainRoot) ? freq.Domain : char.ToUpper(domainRoot[0]) + domainRoot[1..];
            suggestions.Add(new RuleSuggestion(freq, titled, titled));
        }
        return Result.Ok(suggestions);
    }

    public void Dismiss(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return;
        lock (_gate)
        {
            var set = LoadDismissed();
            set.Add(domain);
            SaveDismissed(set);
            _dismissedCache = set;
        }
    }

    public bool IsDismissed(string domain) => LoadDismissed().Contains(domain);

    // --------------------------------------------------------------------- //
    // Helpers
    // --------------------------------------------------------------------- //

    /// <summary>Pull the From-condition values out of every enabled rule and
    /// return them as a domain set we can filter against.</summary>
    private static HashSet<string> ExtractCoveredDomains(IReadOnlyList<MailRule> rules)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules.Where(r => r.IsEnabled))
        {
            foreach (var cond in rule.Conditions.Where(c => c.Field == RuleField.From && !string.IsNullOrWhiteSpace(c.Value)))
            {
                // Tokens are domain-like fragments; the user might have typed
                // "@mobilesentrix.com" or just "mobilesentrix.com" or even
                // "mobilesentrix" — any of those should "cover" the domain.
                var v = cond.Value.Trim().Trim('@');
                if (v.Length >= 3) set.Add(v);
            }
        }
        return set;
    }

    private HashSet<string> LoadDismissed()
    {
        lock (_gate)
        {
            if (_dismissedCache is not null) return _dismissedCache;
            try
            {
                if (!File.Exists(AppPaths.DismissedSuggestionsJson))
                    return _dismissedCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var json = File.ReadAllText(AppPaths.DismissedSuggestionsJson);
                var list = JsonSerializer.Deserialize<List<string>>(json) ?? new();
                return _dismissedCache = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return _dismissedCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static void SaveDismissed(HashSet<string> set)
    {
        try
        {
            AppPaths.EnsureRoot();
            var json = JsonSerializer.Serialize(set.ToList());
            File.WriteAllText(AppPaths.DismissedSuggestionsJson, json);
        }
        catch (Exception ex) { Console.Error.WriteLine($"[RuleSuggestionEngine.Save] {ex.Message}"); }
    }
}
