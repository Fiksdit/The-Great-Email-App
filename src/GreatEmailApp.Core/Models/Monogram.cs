// FILE: src/GreatEmailApp.Core/Models/Monogram.cs
// Created: 2026-11-21 | Revised: 2026-11-21 | Rev: 1
// Changed by: Claude Opus 5.5 on behalf of James Reed
// Single source of truth for avatar initials (account rows + message senders).
// FIX-2026-11-21-001: the old per-file copies split on '.' but not '@', so an
// email-only name like "jreed@fiksdit.com" became "jreed@fiksdit" + "com" → "JC".

namespace GreatEmailApp.Core.Models;

public static class Monogram
{
    private static readonly char[] Separators = { ' ', '.', '-', '_' };

    /// <summary>
    /// Two-letter initials for a display name or email address. For an email
    /// address only the local part (before '@') is used — the domain never
    /// names the person. "James Reed" → "JR", "jreed@fiksdit.com" → "JR",
    /// "james@fixlogic.co" → "JA", "john.smith@x.com" → "JS".
    /// </summary>
    public static string From(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var s = name.Trim();
        var at = s.IndexOf('@');
        if (at > 0) s = s[..at];

        var parts = s.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return (parts[0][0].ToString() + parts[1][0]).ToUpperInvariant();
    }

    /// <summary>Initials for an account: display name if set, else the email address.</summary>
    public static string ForAccount(string? displayName, string? emailAddress) =>
        From(string.IsNullOrWhiteSpace(displayName) ? emailAddress : displayName);
}
