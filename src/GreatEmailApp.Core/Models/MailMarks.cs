// FILE: src/GreatEmailApp.Core/Models/MailMarks.cs
// Created: 2026-11-27 | Revised: 2026-11-27 | Rev: 1
// Changed by: Claude Opus 5.5 on behalf of James Reed
//
// The two per-message markers the user triages with:
//   Flagged  — the standard IMAP \Flagged flag. The user's own "look at this
//              later / follow up" marker; also shows as a flag in Outlook.
//   Priority — an IMAP keyword set by "Mark priority" rules (e.g. Federated
//              mail). Lets rule-marked mail be cleared in bulk without
//              touching the user's review flags. Outlook doesn't show it.

namespace GreatEmailApp.Core.Models;

public enum MailMark { Flagged, Priority }

public static class MailMarks
{
    /// <summary>IMAP keyword stored on the server for <see cref="MailMark.Priority"/>.</summary>
    public const string PriorityKeyword = "Priority";
}
