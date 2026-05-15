// FILE: src/GreatEmailApp.Core/Spam/SpamVerdict.cs
// Created: 2026-05-15 | Revised: 2026-05-15 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Spam;

public enum SpamKind
{
    Clean,       // not spam; deliver normally
    Suspicious,  // some signals fired, score below threshold; deliver but record
    Spam,        // crossed the threshold; auto-move to Junk
}

/// <summary>
/// Outcome of running SpamFilter.Classify against a single message.
/// Score is 0–100 (clamped). Reasons is non-null; useful for audit log + future UI.
/// </summary>
public sealed record SpamVerdict(SpamKind Kind, int Score, IReadOnlyList<string> Reasons);
