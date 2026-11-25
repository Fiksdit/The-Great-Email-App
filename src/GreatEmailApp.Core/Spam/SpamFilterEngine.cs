// FILE: src/GreatEmailApp.Core/Spam/SpamFilterEngine.cs
// Created: 2026-05-15 | Revised: 2026-11-25 | Rev: 5
// Changed by: Claude Opus 5.5 on behalf of James Reed
//
// Orchestrates the spam filter. Wiring mirrors RulesEngine exactly: subscribe
// to NewMailPoller.MessagesPolled, classify each message, act on Spam verdicts
// by setting \Seen then moving to SpamFilterConfig.DestinationFolder (default
// "All Spam/TGEA", created if missing; empty = the server's Junk folder). Both
// engines coexist; if RulesEngine moves a message before we see it, our move
// silently fails and the next poll won't see it again — same fail-safe shape.
//
// First-run bootstrap: if TrustedSenders is empty and TrustedBootstrapDone is
// false, kick off a background scan of each account's Sent folder (one-time)
// to extract recipient addresses into TrustedSenders. Avoids false positives
// on anyone the user has already corresponded with.

using System.Text.RegularExpressions;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Notifications;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Spam;

public sealed class SpamFilterEngine : ISpamFilterEngine
{
    // Email-address extractor used for the Sent-folder bootstrap. Conservative
    // regex: no IDN, no quoted local-parts. "Name <user@host.tld>" -> "user@host.tld";
    // "user@host.tld" -> itself.
    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled);

    private readonly ISpamConfigStore _configStore;
    private readonly ISpamFilter _filter;
    private readonly IAccountStore _accounts;
    private readonly ICredentialStore _creds;
    private readonly IImapService _imap;
    private readonly INewMailPoller _poller;
    private bool _running;

    // Learning filter — see SpamLearningModel. Local file, never synced.
    private readonly string _modelPath;
    private readonly SpamLearningModel _model;

    public SpamFilterEngine(
        ISpamConfigStore configStore,
        ISpamFilter filter,
        IAccountStore accounts,
        ICredentialStore creds,
        IImapService imap,
        INewMailPoller poller,
        string? modelPath = null)
    {
        _configStore = configStore;
        _filter = filter;
        _accounts = accounts;
        _creds = creds;
        _imap = imap;
        _poller = poller;
        _modelPath = modelPath ?? Storage.AppPaths.SpamModelJson;
        _model = SpamLearningModel.Load(_modelPath);
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _poller.MessagesPolled += OnMessagesPolled;

        // Kick off trusted-list bootstrap if it hasn't run yet. Fire-and-forget;
        // the engine works fine without it (just more permissive false-positive
        // risk on senders the user knows but hasn't received from recently).
        _ = Task.Run(BootstrapTrustedSendersAsync);

        // First-time training of the learning filter (no-op once trained).
        _ = Task.Run(() => EnsureModelTrainedAsync(force: false, CancellationToken.None));
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _poller.MessagesPolled -= OnMessagesPolled;
    }

    private async void OnMessagesPolled(object? sender, MessagesPolledEvent ev)
    {
        // Retry an unfinished trusted-list bootstrap (e.g. it first ran before
        // the passwords were restored on a new PC). Cheap no-op once done.
        _ = Task.Run(BootstrapTrustedSendersAsync);
        // Same for first-time learning-model training (needs every password).
        _ = Task.Run(() => EnsureModelTrainedAsync(force: false, CancellationToken.None));

        try { await ApplyAsync(ev.Account, ev.FolderPath, ev.Messages); }
        catch (Exception ex) { Console.Error.WriteLine($"[SpamFilterEngine.OnMessagesPolled] {ex.Message}"); }
    }

    // --------------------------------------------------------------------- //
    // Learning filter
    // --------------------------------------------------------------------- //

    public SpamModelStatus LearningStatus => _model.Status;
    public bool IsTraining => Volatile.Read(ref _trainingRunning) == 1;
    public event EventHandler? LearningStatusChanged;

    // 0 = idle, 1 = training from folders.
    private int _trainingRunning;

    private HashSet<string> OwnDomains() =>
        _accounts.LoadAll()
            .Select(a => (a.EmailAddress ?? "").Split('@').LastOrDefault() ?? "")
            .Where(d => d.Length > 0)
            .Select(d => d.ToLowerInvariant())
            .ToHashSet();

    /// <summary>
    /// Learning-filter verdict. Trusted senders are never flagged; the model
    /// only judges once it has enough examples of both kinds.
    /// </summary>
    private bool IsLearnedSpam(Message msg, SpamFilterConfig config, IReadOnlySet<string> own)
    {
        if (!config.LearningEnabled) return false;
        if (SpamFilter.IsTrustedSender(msg.SenderEmail, config)) return false;
        if (IsKnownCorrespondentDomain(msg.SenderEmail, config, own, _model)) return false;
        var score = _model.Score(msg, own);
        return score is double s && s > config.LearningThreshold;
    }

    // Shared mailbox providers: one trusted gmail.com friend says nothing about
    // the next gmail.com sender, so these never count as a known company domain.
    private static readonly HashSet<string> SharedMailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "yahoo.com", "ymail.com", "rocketmail.com",
        "outlook.com", "hotmail.com", "live.com", "msn.com", "passport.com",
        "aol.com", "icloud.com", "me.com", "mac.com", "protonmail.com", "proton.me",
        "pm.me", "gmx.com", "gmx.net", "mail.com", "zoho.com", "yandex.com",
        "comcast.net", "att.net", "sbcglobal.net", "verizon.net", "bellsouth.net",
        "cox.net", "charter.net", "earthlink.net", "fastmail.com", "hey.com",
    };

    /// <summary>
    /// FIX-2026-11-25-001: the learning filter judged a reply from a new
    /// Federated Service address as spam on its wording alone ("told", "going",
    /// "just" are common in the owner's spam), although 104 filed messages come
    /// from that company. A sender's domain counts as known — and the learning
    /// layer stays out of it — when someone at that domain is trusted, or the
    /// user has filed plenty of its mail and almost none of it was spam.
    /// Never for the user's own domains (spoofed in phishing) or shared mailbox
    /// providers. The keyword filter and blocked list still apply.
    /// </summary>
    public static bool IsKnownCorrespondentDomain(string? email, SpamFilterConfig config,
        IReadOnlySet<string> ownDomains, SpamLearningModel model)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        var at = e.LastIndexOf('@');
        if (at <= 0 || at == e.Length - 1) return false;
        var domain = e[(at + 1)..];
        if (ownDomains.Contains(domain) || SharedMailDomains.Contains(domain)) return false;

        // Someone at this domain is trusted (an address, or the domain itself).
        foreach (var entry in config.TrustedSenders)
        {
            var t = (entry ?? "").Trim();
            var tAt = t.LastIndexOf('@');
            if (tAt >= 0 && t[(tAt + 1)..].Equals(domain, StringComparison.OrdinalIgnoreCase)) return true;
        }

        // Filed history: at least 10 kept messages, and good outnumbers spam 10 to 1.
        var (spam, good) = model.DomainCounts(domain);
        return good >= 10 && good >= spam * 10;
    }

    public void Learn(Message message, bool isSpam)
    {
        // A user correction counts double — it's the most reliable label we get.
        _model.Learn(message, isSpam, OwnDomains(), weight: 2);
        try { _model.Save(_modelPath); }
        catch (Exception ex) { Console.Error.WriteLine($"[SpamFilterEngine.Learn] {ex.Message}"); }
        LearningStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<Result<SpamModelStatus>> RetrainAsync(CancellationToken ct = default)
    {
        var r = await EnsureModelTrainedAsync(force: true, ct).ConfigureAwait(false);
        return r;
    }

    /// <summary>
    /// Train from the server folders. Spam examples: every folder whose path
    /// contains "spam" or "junk" (the user's All Spam/* filters, the server's
    /// spam folder, TGEA). Good examples: the user's own filed folders (not
    /// Inbox — it holds unfiltered spam — nor Trash/Sent/Drafts/Archive).
    /// Read-only. Runs once (needs every account's password, so on a new PC it
    /// waits for the vault unlock and retries each poll), or on Retrain.
    /// </summary>
    private async Task<Result<SpamModelStatus>> EnsureModelTrainedAsync(bool force, CancellationToken ct)
    {
        if (!force && _model.Status.TrainedAt is not null)
            return Result.Ok(_model.Status);
        if (Interlocked.CompareExchange(ref _trainingRunning, 1, 0) != 0)
            return Result.Fail<SpamModelStatus>("Training is already running.");
        LearningStatusChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            var accounts = _accounts.LoadAll();
            if (accounts.Count == 0) return Result.Fail<SpamModelStatus>("No accounts.");
            if (accounts.Any(a => _creds.Read(a.Id) is null))
                return Result.Fail<SpamModelStatus>("Waiting for every account's password before training.");

            var own = OwnDomains();
            var fresh = new SpamLearningModel();
            foreach (var account in accounts)
            {
                ct.ThrowIfCancellationRequested();
                var pw = _creds.Read(account.Id)!.Value.Password;
                var folders = await _imap.ListFoldersAsync(account, pw, ct).ConfigureAwait(false);
                if (folders is not Result<List<Folder>>.Ok fOk)
                    return Result.Fail<SpamModelStatus>($"Couldn't list folders for {account.EmailAddress}.");

                foreach (var f in Flatten(fOk.Value))
                {
                    var kind = TrainingKind(f);
                    if (kind is null) continue;
                    // §14 limits: newest 500 per spam folder, 60 per filed folder.
                    var list = await _imap.ListMessagesAsync(account, pw, f.FullPath,
                        kind.Value ? 500 : 60, ct).ConfigureAwait(false);
                    if (list is not Result<List<Message>>.Ok mOk) continue;
                    foreach (var m in mOk.Value) fresh.Learn(m, kind.Value, own);
                }
            }

            // Swap in the freshly trained counts (locked — the poller may be scoring).
            _model.ReplaceWith(fresh);
            _model.Save(_modelPath);
            Console.Error.WriteLine($"[SpamFilterEngine.Train] spam={fresh.SpamDocs} good={fresh.HamDocs}");
            return Result.Ok(_model.Status);
        }
        catch (OperationCanceledException) { return Result.Fail<SpamModelStatus>("Training cancelled."); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SpamFilterEngine.Train] {ex.Message}");
            return Result.Fail<SpamModelStatus>($"Training failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _trainingRunning, 0);
            LearningStatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>true = spam example, false = good example, null = skip.</summary>
    private static bool? TrainingKind(Folder f)
    {
        if (string.IsNullOrEmpty(f.FullPath)) return null; // synthetic (Outbox)
        var path = f.FullPath;
        if (path.Contains("spam", StringComparison.OrdinalIgnoreCase)
            || path.Contains("junk", StringComparison.OrdinalIgnoreCase)
            || f.Special == SpecialFolder.Junk)
        {
            // FIX-2026-11-25-001: a spam folder that holds other spam folders
            // (the owner's "All Spam") is a container, not a filter's target —
            // mail sitting directly in it isn't reliably spam (another mail
            // program moved ~100 order/work emails there in Nov 2026). Learning
            // from it would teach the filter that Amazon and clients are spam.
            if (f.Children.Count > 0 && f.Special != SpecialFolder.Junk) return null;
            return true;
        }
        if (f.Special != SpecialFolder.None) return null; // Inbox, Sent, Drafts, Trash, Archive
        if (f.Name.Equals("Trash", StringComparison.OrdinalIgnoreCase)
            || f.Name.StartsWith("Sent", StringComparison.OrdinalIgnoreCase)
            || f.Name.Equals("Drafts", StringComparison.OrdinalIgnoreCase)
            || f.Name.Equals("Deleted", StringComparison.OrdinalIgnoreCase))
            return null;
        return false;
    }

    private static IEnumerable<Folder> Flatten(IEnumerable<Folder> folders)
    {
        foreach (var f in folders)
        {
            yield return f;
            foreach (var c in Flatten(f.Children)) yield return c;
        }
    }

    // 0 = idle, 1 = a bootstrap scan is running (Start + every poll can trigger it).
    private int _bootstrapRunning;

    // Accounts already scanned successfully this session — a retry (after a
    // password is restored on one account) only re-scans the ones still missing.
    private readonly HashSet<string> _bootstrapScanned = new();

    /// <summary>
    /// Bootstrap still needed? Not done yet — or "done" with an EMPTY list,
    /// which is what the pre-fix bootstrap left behind when it ran before any
    /// account had a password (FIX-2026-11-21-003). Anyone with a Sent folder
    /// has recipients, so an empty completed list is never a real result.
    /// </summary>
    private static bool NeedsBootstrap(SpamFilterConfig c) =>
        !c.TrustedBootstrapDone || c.TrustedSenders.Count == 0;

    public async Task<Result<SpamFilterResult>> ApplyAsync(
        Account account, string folderPath, IEnumerable<Message> messages,
        CancellationToken ct = default)
    {
        var config = _configStore.Load();
        if (!config.Enabled)
            return Result.Ok(new SpamFilterResult(0, 0, new List<string>()));

        var creds = _creds.Read(account.Id);
        if (creds is null)
            return Result.Fail<SpamFilterResult>($"No password stored for {account.EmailAddress}.");

        int considered = 0, filtered = 0;
        var errors = new List<string>();
        var own = OwnDomains();

        foreach (var msg in messages)
        {
            ct.ThrowIfCancellationRequested();
            if (!uint.TryParse(msg.Id, out var uid)) continue;
            // Skip messages already marked read — those are old (the poller
            // surfaces unread-only as "new"), and re-filtering history is a
            // separate Phase 2 action.
            if (!msg.Unread) continue;
            considered++;

            var verdict = _filter.Classify(msg, config);
            if (verdict.Kind != SpamKind.Spam && !IsLearnedSpam(msg, config, own)) continue;

            // Mark seen FIRST so the user never sees a Junk unread count tick
            // up. The move below invalidates the UID, so order matters.
            var seen = await _imap.SetSeenAsync(account, creds.Value.Password, folderPath, uid, true, ct)
                .ConfigureAwait(false);
            if (seen is Result<bool>.Fail seenFail)
            {
                errors.Add($"SetSeen failed for uid={uid}: {seenFail.Error}");
                continue;
            }

            // App catches go to their own folder (default "All Spam/TGEA",
            // created if missing) so they're distinguishable from the server's
            // and Outlook's spam handling. Empty setting = server Junk folder.
            var segments = (config.DestinationFolder ?? "")
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var moved = segments.Length == 0
                ? await _imap.MoveToSpecialAsync(account, creds.Value.Password, folderPath, uid,
                    SpecialFolder.Junk, ct).ConfigureAwait(false)
                : await _imap.MoveToNamedPathAsync(account, creds.Value.Password, folderPath, uid,
                    segments, createIfMissing: true, ct).ConfigureAwait(false);
            if (moved is Result<string>.Ok)
            {
                filtered++;
            }
            else if (moved is Result<string>.Fail moveFail)
            {
                errors.Add($"Move to spam folder failed for uid={uid}: {moveFail.Error}");
            }
        }

        return Result.Ok(new SpamFilterResult(considered, filtered, errors));
    }

    // --------------------------------------------------------------------- //
    // Trusted-from-Sent bootstrap. One-shot. Best-effort.
    // --------------------------------------------------------------------- //

    private async Task BootstrapTrustedSendersAsync()
    {
        if (Interlocked.CompareExchange(ref _bootstrapRunning, 1, 0) != 0) return;
        try
        {
            if (!NeedsBootstrap(_configStore.Load())) return;

            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var accounts = _accounts.LoadAll();

            // Never trust the user's own addresses: spoofed "from yourself"
            // mailbox-quota phishing is a known pattern, and a self-addressed
            // message in Sent would otherwise whitelist it.
            var own = new HashSet<string>(accounts.Select(a => a.EmailAddress), StringComparer.OrdinalIgnoreCase);

            // FIX-2026-11-21-003: only a scan that actually reached every
            // account counts as done. The old code `continue`d past accounts
            // with no password yet (new PC, before vault unlock), then marked
            // the bootstrap done with an empty list — permanently.
            bool complete = true;

            foreach (var account in accounts)
            {
                if (_bootstrapScanned.Contains(account.Id)) continue;
                var creds = _creds.Read(account.Id);
                if (creds is null) { complete = false; continue; }

                // Find the Sent folder for this account. ListFoldersAsync's
                // Folder.Special is populated from the server's \Sent flag.
                var folders = await _imap.ListFoldersAsync(account, creds.Value.Password).ConfigureAwait(false);
                if (folders is not Result<List<Folder>>.Ok foldersOk) { complete = false; continue; }

                var sent = FindSent(foldersOk.Value);
                if (sent is null) { _bootstrapScanned.Add(account.Id); continue; } // nothing to learn from — still counts as scanned

                // Pull a generous slice — first 500 Sent envelopes is plenty
                // to seed a useful trust list without dragging on for minutes.
                var msgs = await _imap.ListMessagesAsync(account, creds.Value.Password,
                    sent.FullPath, 500).ConfigureAwait(false);
                if (msgs is not Result<List<Message>>.Ok msgsOk) { complete = false; continue; }
                _bootstrapScanned.Add(account.Id);

                foreach (var m in msgsOk.Value)
                {
                    // Outgoing message: To/Cc fields hold recipients we trust.
                    foreach (Match match in EmailRegex.Matches($"{m.To} {m.Cc}"))
                    {
                        var addr = match.Value.ToLowerInvariant();
                        if (!own.Contains(addr)) found.Add(addr);
                    }
                }
            }

            // Re-load right before saving and MERGE, so edits made in Settings
            // (or pulled by sync) during the scan aren't overwritten.
            var config = _configStore.Load();
            var merged = new HashSet<string>(config.TrustedSenders, StringComparer.OrdinalIgnoreCase);
            int added = found.Count(a => merged.Add(a));
            if (added == 0 && !complete) return; // nothing new; retry next poll

            config.TrustedSenders = merged.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
            config.TrustedBootstrapDone = complete;
            _configStore.Save(config);

            Console.Error.WriteLine($"[SpamFilterEngine.Bootstrap] Added {added} trusted sender(s) (total {merged.Count}); complete={complete}.");
        }
        catch (Exception ex)
        {
            // Failure here is non-fatal — the filter just runs with whatever
            // trust list is present. Done stays false, so the next poll retries.
            Console.Error.WriteLine($"[SpamFilterEngine.Bootstrap] {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _bootstrapRunning, 0);
        }
    }

    private static Folder? FindSent(IEnumerable<Folder> folders)
    {
        foreach (var f in folders)
        {
            if (f.Special == SpecialFolder.Sent) return f;
            var nested = FindSent(f.Children);
            if (nested is not null) return nested;
        }
        return null;
    }
}
