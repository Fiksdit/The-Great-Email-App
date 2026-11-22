// FILE: src/GreatEmailApp.Core/Rules/IRulesEngine.cs
// Created: 2026-05-01 | Revised: 2026-11-21 | Rev: 3
// Changed by: Claude Opus 5.5 on behalf of James Reed

using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Rules;

public sealed record RuleApplyResult(int Considered, int Matched, int ActionsRun, List<string> Errors);

public interface IRulesEngine
{
    /// <summary>Subscribe to NewMailPoller.MessagesPolled at startup so rules
    /// run on every freshly-arrived inbox listing. Idempotent.</summary>
    void Start();
    void Stop();

    /// <summary>Run all enabled rules against the given message set, returning
    /// a summary. Used by the "Run rules now" button + the auto-run on poll.
    /// <paramref name="newUids"/>: UIDs that just arrived (from the poller) —
    /// rules marked IncomingOnly act only on these. Null (manual run, or a
    /// first poll with no baseline) means IncomingOnly rules are skipped.
    /// <paramref name="ruleFilter"/>: optional extra filter (e.g. "Spam rules
    /// only" from the Run rules now dialog).</summary>
    Task<Result<RuleApplyResult>> ApplyAsync(Account account, string folderPath,
        IEnumerable<Message> messages, CancellationToken ct = default,
        IReadOnlySet<uint>? newUids = null, Func<MailRule, bool>? ruleFilter = null);
}
