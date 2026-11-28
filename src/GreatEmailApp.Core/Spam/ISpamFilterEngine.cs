// FILE: src/GreatEmailApp.Core/Spam/ISpamFilterEngine.cs
// Created: 2026-05-15 | Revised: 2026-11-21 | Rev: 2
// Changed by: Claude Opus 5.5 on behalf of James Reed

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

    // ----- Learning filter ----- //

    /// <summary>Current size/readiness of the learning model.</summary>
    SpamModelStatus LearningStatus { get; }

    /// <summary>True while the learning model is (re)training from folders.</summary>
    bool IsTraining { get; }

    /// <summary>Raised (any thread) when training starts/finishes or the model changes.</summary>
    event EventHandler? LearningStatusChanged;

    /// <summary>Teach the model from a user decision (Mark as spam / Not spam).</summary>
    void Learn(Message message, bool isSpam);

    /// <summary>What each filter layer makes of this message right now (for the spam review log).</summary>
    SpamExplanation Explain(Message message);

    /// <summary>Discard the model and rebuild it from the accounts' spam and filed folders.</summary>
    Task<Result<SpamModelStatus>> RetrainAsync(CancellationToken ct = default);
}
