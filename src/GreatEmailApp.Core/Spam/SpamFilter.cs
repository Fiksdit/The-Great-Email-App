// FILE: src/GreatEmailApp.Core/Spam/SpamFilter.cs
// Created: 2026-05-15 | Revised: 2026-05-15 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Keyword + heuristic spam classifier. Scoring is intentionally simple — we
// want easy-to-explain "why was this junked?" reasons in the verdict.
// All rules add to a 0-100 score, clamped at the ends. TrustedSenders short-
// circuits with Clean; BlockedSenders short-circuits with score=100.
//
// Order of evaluation:
//   1. Allowlist check  -> immediate Clean
//   2. Blocklist check  -> immediate Spam (score 100, single reason)
//   3. Keyword sweep (subject hits worth more than preview hits)
//   4. Heuristic sweep (all-caps subject, excessive punctuation, throwaway
//      sender display name, mostly-digit local part)
//   5. Compare final score to ThresholdScore -> Spam / Suspicious / Clean
//
// What we deliberately DON'T look at in Phase 1:
//   - Full body (BodyHtml/BodyPlain are populated lazily via FetchBodyAsync;
//     not available at poll time, only Preview is)
//   - X-Spam-Score / X-Spam-Flag headers (Message.cs doesn't expose them;
//     adding that is a downstream ImapService change for a future phase)

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Spam;

public sealed class SpamFilter : ISpamFilter
{
    // Per-rule weights. Tuned so two strong subject keyword hits OR one keyword
    // hit plus a caps-shouting subject crosses the default 70 threshold.
    private const int KeywordSubjectHit       = 40;
    private const int KeywordPreviewHit       = 25;
    private const int AllCapsSubjectBonus     = 30;
    private const int ExcessivePunctuationBonus = 25;
    private const int EmptyDisplayNameBonus   = 5;
    private const int NumericLocalPartBonus   = 20;

    public SpamVerdict Classify(Message message, SpamFilterConfig config)
    {
        if (message is null) return new SpamVerdict(SpamKind.Clean, 0, Array.Empty<string>());

        // 1. Allowlist — short-circuit. Trusted senders are never spam, even
        // if their email contains "viagra" verbatim. Protects against false
        // positives from real correspondents discussing pharmacy, payments,
        // verification, etc.
        if (IsSenderInList(message.SenderEmail, config.TrustedSenders))
            return new SpamVerdict(SpamKind.Clean, 0, new[] { "Sender is in trusted list" });

        var reasons = new List<string>();

        // 2. Blocklist — opposite short-circuit.
        if (IsSenderInList(message.SenderEmail, config.BlockedSenders))
        {
            reasons.Add("Sender is in blocked list");
            return new SpamVerdict(SpamKind.Spam, 100, reasons);
        }

        int score = 0;
        var subject = (message.Subject ?? "").Trim();
        var preview = (message.Preview ?? "").Trim();
        var senderEmail = (message.SenderEmail ?? "").Trim();
        var senderName = (message.Sender ?? "").Trim();

        // 3. Keyword sweep — case-insensitive substring match against subject
        // and preview. Subject hits weighted higher because spammers can't
        // hide their pitch from the subject line, but can pad the preview.
        foreach (var kw in config.SubjectKeywords)
        {
            if (string.IsNullOrWhiteSpace(kw)) continue;
            if (subject.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                score += KeywordSubjectHit;
                reasons.Add($"Subject contains \"{kw}\"");
            }
            else if (preview.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                score += KeywordPreviewHit;
                reasons.Add($"Preview contains \"{kw}\"");
            }
        }

        // 4. Heuristics.

        // 4a. Subject shouting. Counts only letters so "RE: " prefixes and
        // digits don't disqualify a normal reply. >5 letters required so
        // legitimate abbreviations ("FW", "OT", "FYI") don't trigger.
        if (IsShouting(subject))
        {
            score += AllCapsSubjectBonus;
            reasons.Add("Subject is ALL CAPS");
        }

        // 4b. Excessive punctuation (!!!, ???, $$$, !!!!!). Triggers on 3+
        // consecutive of the same special char.
        if (HasExcessivePunctuation(subject))
        {
            score += ExcessivePunctuationBonus;
            reasons.Add("Subject has excessive punctuation");
        }

        // 4c. Empty display name — many newsletters and real senders also
        // omit it, so this is only a small bonus.
        if (string.IsNullOrWhiteSpace(senderName)
            || string.Equals(senderName, senderEmail, StringComparison.OrdinalIgnoreCase))
        {
            score += EmptyDisplayNameBonus;
            reasons.Add("Sender has no display name");
        }

        // 4d. Mostly-numeric local-part — "john8472923@whatever.tld" is far
        // more often a throwaway spam account than a real correspondent.
        if (HasMostlyNumericLocalPart(senderEmail))
        {
            score += NumericLocalPartBonus;
            reasons.Add("Sender local-part is mostly digits");
        }

        // 5. Clamp + decide.
        if (score < 0) score = 0;
        if (score > 100) score = 100;

        var kind = score >= config.ThresholdScore ? SpamKind.Spam
                 : score > 0 ? SpamKind.Suspicious
                 : SpamKind.Clean;

        return new SpamVerdict(kind, score, reasons);
    }

    // --------------------------------------------------------------------- //
    // Helpers
    // --------------------------------------------------------------------- //

    /// <summary>
    /// Match an email against a list of either full addresses ("foo@bar.com")
    /// or domain entries with leading "@" ("@bar.com"). Case-insensitive.
    /// </summary>
    private static bool IsSenderInList(string? email, IReadOnlyList<string> list)
    {
        if (string.IsNullOrWhiteSpace(email) || list.Count == 0) return false;
        var e = email.Trim();
        foreach (var entry in list)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            var t = entry.Trim();
            if (t.StartsWith("@"))
            {
                if (e.EndsWith(t, StringComparison.OrdinalIgnoreCase)) return true;
            }
            else
            {
                if (e.Equals(t, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    private static bool IsShouting(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        // Strip the standard reply/forward prefix so "RE: HELLO" still counts.
        var trimmed = s;
        while (true)
        {
            var orig = trimmed;
            if (trimmed.StartsWith("re:", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[3..].TrimStart();
            else if (trimmed.StartsWith("fw:", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[3..].TrimStart();
            else if (trimmed.StartsWith("fwd:", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[4..].TrimStart();
            if (trimmed == orig) break;
        }
        int letters = 0, upper = 0;
        foreach (var c in trimmed)
        {
            if (char.IsLetter(c))
            {
                letters++;
                if (char.IsUpper(c)) upper++;
            }
        }
        // Need a meaningful amount of text — short bursts like "OK" or "FYI"
        // don't qualify. >=6 letters and >=90% uppercase.
        return letters >= 6 && upper * 10 >= letters * 9;
    }

    private static bool HasExcessivePunctuation(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        int run = 1;
        for (int i = 1; i < s.Length; i++)
        {
            char c = s[i];
            if ((c == '!' || c == '?' || c == '$') && s[i] == s[i - 1])
            {
                run++;
                if (run >= 3) return true;
            }
            else
            {
                run = 1;
            }
        }
        return false;
    }

    private static bool HasMostlyNumericLocalPart(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        int at = email.IndexOf('@');
        if (at <= 0) return false;
        var local = email[..at];
        // Need at least 6 chars; "j2024" is fine, "j83274982" is suspect.
        if (local.Length < 6) return false;
        int digits = 0, letters = 0;
        foreach (var c in local)
        {
            if (char.IsDigit(c)) digits++;
            else if (char.IsLetter(c)) letters++;
        }
        // Trigger when at least 60% of alphanumerics are digits AND there's
        // a meaningful number of digits (>=4) so "h2g2" passes.
        return digits >= 4 && digits * 10 >= (digits + letters) * 6;
    }
}
