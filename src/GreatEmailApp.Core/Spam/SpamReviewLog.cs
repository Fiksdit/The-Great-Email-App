// FILE: src/GreatEmailApp.Core/Spam/SpamReviewLog.cs
// Created: 2026-11-27 | Revised: 2026-11-27 | Rev: 1
// Changed by: Claude Opus 5.5 on behalf of James Reed
//
// Every message the user marks as spam by hand is a filter miss. This log
// keeps one JSON line per miss — who sent it, what it said, and what each
// filter layer thought of it at that moment (SpamFilterEngine.Explain) — so
// misses can be reviewed in bulk and turned into keyword / rule / threshold
// fixes. Local-only file: spam-review.jsonl next to the other app data.

using System.Text.Json;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Spam;

public sealed record SpamExplanation(
    string KeywordVerdict, int KeywordScore, List<string> KeywordReasons,
    double? LearnedScore, double LearnedThreshold,
    bool TrustedSender, bool KnownCompanyDomain,
    List<string> WhyNotCaught);

public sealed record SpamReviewEntry(
    DateTimeOffset MarkedAt, string Account, string Folder, string Uid,
    string Sender, string SenderEmail, string Subject, string Preview,
    DateTimeOffset? SentAt, SpamExplanation Filter);

public static class SpamReviewLog
{
    public static string PathName => Path.Combine(AppPaths.Root, "spam-review.jsonl");
    private static readonly object Gate = new();

    public static void Append(Message m, string accountEmail, SpamExplanation explanation)
    {
        var entry = new SpamReviewEntry(DateTimeOffset.Now, accountEmail, m.FolderId, m.Id,
            m.Sender, m.SenderEmail, m.Subject, m.Preview, m.SentAt, explanation);
        try
        {
            AppPaths.EnsureRoot();
            var line = JsonSerializer.Serialize(entry);
            lock (Gate) File.AppendAllText(PathName, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SpamReviewLog] {ex.Message}");
        }
    }
}
