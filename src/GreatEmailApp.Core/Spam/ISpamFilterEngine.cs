// FILE: src/GreatEmailApp.Core/Spam/ISpamFilterEngine.cs
// Created: 2026-05-15 | Revised: 2026-05-15 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Spam;

public sealed record SpamFilterResult(int Considered, int Filtered, List<string> Errors);

public interface ISpamFilterEngine
{
    /// <summary>Subscribe to NewMailPoller.MessagesPolled so every poll cycle
    /// classifies new messages and moves spam to Junk. Idempotent.</summary>
    void Start();
    void Stop();

    /// <summary>Run the filter manually against a supplied message set. Used
    /// by future "Re-filter inbox" Settings action — not wired in Phase 1.</summary>
    Task<Result<SpamFilterResult>> ApplyAsync(Account account, string folderPath,
        IEnumerable<Message> messages, CancellationToken ct = default);
}
