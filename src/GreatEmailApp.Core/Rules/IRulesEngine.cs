// FILE: src/GreatEmailApp.Core/Rules/IRulesEngine.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

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
    /// a summary. Used by the "Run rules now" button + the auto-run on poll.</summary>
    Task<Result<RuleApplyResult>> ApplyAsync(Account account, string folderPath,
        IEnumerable<Message> messages, CancellationToken ct = default);
}
