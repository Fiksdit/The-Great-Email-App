// FILE: src/GreatEmailApp.Core/Spam/SpamLearningModel.cs
// Created: 2026-11-21 | Revised: 2026-11-21 | Rev: 1
// Changed by: Claude Opus 5.5 on behalf of James Reed
//
// Learning spam filter (naive Bayes over message tokens). Learns which words,
// sender domains and domain extensions show up in the user's spam vs. the mail
// they keep. Validated on the owner's mail 2026-11-21 (5-fold, trusted senders
// exempt, own domain not a feature): 92% of spam caught at strictness 12 with
// 0.8% of filed good mail flagged; the keyword filter caught 0%.
//
// Local-only (spam-model.json, never synced): each PC trains from the same
// server folders, and the model is too big/chatty for the Firestore doc.
// Thread-safe: the poller thread scores while the UI thread learns.

using System.Text.Json;
using System.Text.RegularExpressions;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Spam;

public sealed record SpamModelStatus(int SpamExamples, int GoodExamples, DateTimeOffset? TrainedAt, bool IsReady);

public sealed class SpamLearningModel
{
    /// <summary>Minimum examples of each kind before the model is allowed to judge.</summary>
    public const int MinExamplesEach = 30;

    // Persisted state (public for System.Text.Json).
    public int SpamDocs { get; set; }
    public int HamDocs { get; set; }
    public Dictionary<string, int> SpamCounts { get; set; } = new();
    public Dictionary<string, int> HamCounts { get; set; } = new();
    public DateTimeOffset? TrainedAt { get; set; }

    private readonly object _lock = new();
    private static readonly Regex NonWord = new(@"[^a-z0-9'$%]+", RegexOptions.Compiled);

    public SpamModelStatus Status
    {
        get { lock (_lock) return new(SpamDocs, HamDocs, TrainedAt, IsReadyUnlocked); }
    }

    private bool IsReadyUnlocked => SpamDocs >= MinExamplesEach && HamDocs >= MinExamplesEach;

    /// <summary>
    /// Distinct tokens for one message: words (3–20 chars) from subject,
    /// preview and display name; "tld:.xyz"; "dom:sender.domain" (skipped for
    /// the user's own domains — spoofed in spam AND used by real colleagues,
    /// so it's noise); "feat:lookalike" for I/l look-alike words.
    /// </summary>
    public static HashSet<string> Tokenize(Message m, IReadOnlySet<string> ownDomains)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var text = NonWord.Replace($"{m.Subject} {m.Preview} {m.Sender}".ToLowerInvariant(), " ");
        foreach (var w in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (w.Length is >= 3 and <= 20) tokens.Add(w);

        var email = (m.SenderEmail ?? "").Trim().ToLowerInvariant();
        var at = email.LastIndexOf('@');
        if (at >= 0 && at < email.Length - 1)
        {
            var domain = email[(at + 1)..];
            var dot = domain.LastIndexOf('.');
            if (dot >= 0) tokens.Add("tld:" + domain[dot..]);
            if (!ownDomains.Contains(domain)) tokens.Add("dom:" + domain);
        }
        if (SpamFilter.CountLookAlikeWords(m) > 0) tokens.Add("feat:lookalike");
        return tokens;
    }

    public void Learn(Message m, bool isSpam, IReadOnlySet<string> ownDomains, int weight = 1)
    {
        var tokens = Tokenize(m, ownDomains);
        lock (_lock)
        {
            var counts = isSpam ? SpamCounts : HamCounts;
            foreach (var t in tokens) counts[t] = counts.GetValueOrDefault(t) + weight;
            if (isSpam) SpamDocs += weight; else HamDocs += weight;
        }
    }

    /// <summary>
    /// Log-odds that the message is spam (>0 leans spam). Null until the model
    /// has <see cref="MinExamplesEach"/> examples of both kinds. Tokens never
    /// seen in training are ignored.
    /// </summary>
    public double? Score(Message m, IReadOnlySet<string> ownDomains)
    {
        var tokens = Tokenize(m, ownDomains);
        lock (_lock)
        {
            if (!IsReadyUnlocked) return null;
            double ns = SpamDocs, nh = HamDocs;
            double lp = Math.Log(ns / nh);
            foreach (var t in tokens)
            {
                SpamCounts.TryGetValue(t, out var cs);
                HamCounts.TryGetValue(t, out var ch);
                if (cs == 0 && ch == 0) continue;
                lp += Math.Log(((cs + 0.5) / (ns + 1)) / ((ch + 0.5) / (nh + 1)));
            }
            return lp;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            SpamDocs = HamDocs = 0;
            SpamCounts.Clear();
            HamCounts.Clear();
            TrainedAt = null;
        }
    }

    public void MarkTrained()
    {
        lock (_lock) TrainedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Atomically replace this model's counts with a freshly trained one's.</summary>
    public void ReplaceWith(SpamLearningModel trained)
    {
        lock (_lock)
        {
            SpamDocs = trained.SpamDocs;
            HamDocs = trained.HamDocs;
            SpamCounts = new Dictionary<string, int>(trained.SpamCounts);
            HamCounts = new Dictionary<string, int>(trained.HamCounts);
            TrainedAt = DateTimeOffset.UtcNow;
        }
    }

    // --------------------------------------------------------------------- //
    // Persistence
    // --------------------------------------------------------------------- //

    public static SpamLearningModel Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<SpamLearningModel>(File.ReadAllText(path)) ?? new();
        }
        catch (Exception ex)
        {
            // Corrupt model → start fresh; it retrains from the folders.
            Console.Error.WriteLine($"[SpamLearningModel.Load] {ex.Message}");
        }
        return new();
    }

    public void Save(string path)
    {
        string json;
        lock (_lock) json = JsonSerializer.Serialize(this);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }
}
