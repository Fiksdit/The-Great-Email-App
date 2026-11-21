// FILE: src/GreatEmailApp.Core/Spam/SpamFilterEngine.cs
// Created: 2026-05-15 | Revised: 2026-11-21 | Rev: 3
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

    public SpamFilterEngine(
        ISpamConfigStore configStore,
        ISpamFilter filter,
        IAccountStore accounts,
        ICredentialStore creds,
        IImapService imap,
        INewMailPoller poller)
    {
        _configStore = configStore;
        _filter = filter;
        _accounts = accounts;
        _creds = creds;
        _imap = imap;
        _poller = poller;
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

        try { await ApplyAsync(ev.Account, ev.FolderPath, ev.Messages); }
        catch (Exception ex) { Console.Error.WriteLine($"[SpamFilterEngine.OnMessagesPolled] {ex.Message}"); }
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
            if (verdict.Kind != SpamKind.Spam) continue;

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
