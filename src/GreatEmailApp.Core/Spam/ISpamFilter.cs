// FILE: src/GreatEmailApp.Core/Spam/ISpamFilter.cs
// Created: 2026-05-15 | Revised: 2026-05-15 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Spam;

public interface ISpamFilter
{
    /// <summary>
    /// Score a single message against the supplied config. Pure function —
    /// no IO, no IMAP. Safe to call on a hot loop. Verdict.Kind drives the
    /// engine's action (move to Junk + mark read for Kind = Spam).
    /// </summary>
    SpamVerdict Classify(Message message, SpamFilterConfig config);
}
