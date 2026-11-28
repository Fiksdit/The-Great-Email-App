// FILE: src/GreatEmailApp.Core/Rules/IRulesEngine.cs
// Created: 2026-05-01 | Revised: 2026-11-27 | Rev: 4
// Changed by: Claude Opus 5.5 on behalf of James Reed

using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Rules;

public sealed record RuleApplyResult(int Considered, int Matched, int ActionsRun, List<string> Errors);

/// <summary>A "Mark priority" rule matched a message that just arrived.</summary>
public sealed record PriorityMailEvent(Account Account, Message Message, string RuleName);

public interface IRulesEngine
{
    /// <summary>
    /// Raised (on a worker thread) when a "Mark priority" rule marks newly
    /// arrived mail — drives the priority alert. Not raised for re-scans or
    /// "Run rules now".
    /// </summary>
    event EventHandler<PriorityMailEvent>? PriorityMailArrived;

    /// <summary>Raised after a rules run that flagged or marked priority on any message.</summary>
    event EventHandler? MarksApplied;

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
