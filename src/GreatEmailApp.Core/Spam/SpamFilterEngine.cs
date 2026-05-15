// FILE: src/GreatEmailApp.Core/Spam/SpamFilterEngine.cs
// Created: 2026-05-15 | Revised: 2026-05-15 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Orchestrates the spam filter. Wiring mirrors RulesEngine exactly: subscribe
// to NewMailPoller.MessagesPolled, classify each message, act on Spam verdicts
// by setting \Seen then moving to the account's Junk special folder. Both
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
        try { await ApplyAsync(ev.Account, ev.FolderPath, ev.Messages); }
        catch (Exception ex) { Console.Error.WriteLine($"[SpamFilterEngine.OnMessagesPolled] {ex.Message}"); }
    }

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

            var moved = await _imap.MoveToSpecialAsync(account, creds.Value.Password, folderPath, uid,
                SpecialFolder.Junk, ct).ConfigureAwait(false);
            if (moved is Result<string>.Ok)
            {
                filtered++;
            }
            else if (moved is Result<string>.Fail moveFail)
            {
                errors.Add($"MoveToJunk failed for uid={uid}: {moveFail.Error}");
            }
        }

        return Result.Ok(new SpamFilterResult(considered, filtered, errors));
    }

    // --------------------------------------------------------------------- //
    // Trusted-from-Sent bootstrap. One-shot. Best-effort.
    // --------------------------------------------------------------------- //

    private async Task BootstrapTrustedSendersAsync()
    {
        try
        {
            var config = _configStore.Load();
            if (config.TrustedBootstrapDone) return;

            var seeded = new HashSet<string>(config.TrustedSenders, StringComparer.OrdinalIgnoreCase);
            int added = 0;

            foreach (var account in _accounts.LoadAll())
            {
                var creds = _creds.Read(account.Id);
                if (creds is null) continue;

                // Find the Sent folder for this account. ListFoldersAsync's
                // Folder.Special is populated from the server's \Sent flag.
                var folders = await _imap.ListFoldersAsync(account, creds.Value.Password).ConfigureAwait(false);
                if (folders is not Result<List<Folder>>.Ok foldersOk) continue;

                var sent = FindSent(foldersOk.Value);
                if (sent is null) continue;

                // Pull a generous slice — first 500 Sent envelopes is plenty
                // to seed a useful trust list without dragging on for minutes.
                var msgs = await _imap.ListMessagesAsync(account, creds.Value.Password,
                    sent.FullPath, 500).ConfigureAwait(false);
                if (msgs is not Result<List<Message>>.Ok msgsOk) continue;

                foreach (var m in msgsOk.Value)
                {
                    // Outgoing message: To/Cc fields hold recipients we trust.
                    foreach (Match match in EmailRegex.Matches(m.To ?? ""))
                    {
                        if (seeded.Add(match.Value.ToLowerInvariant())) added++;
                    }
                    foreach (Match match in EmailRegex.Matches(m.Cc ?? ""))
                    {
                        if (seeded.Add(match.Value.ToLowerInvariant())) added++;
                    }
                }
            }

            // Save even if added=0 — we still want TrustedBootstrapDone set so
            // we don't rescan every launch.
            config.TrustedSenders = seeded.ToList();
            config.TrustedBootstrapDone = true;
            _configStore.Save(config);

            Console.Error.WriteLine($"[SpamFilterEngine.Bootstrap] Seeded TrustedSenders with {added} new entries (total {seeded.Count}).");
        }
        catch (Exception ex)
        {
            // Failure here is non-fatal — the filter just runs with whatever
            // trust list is present. Don't flip TrustedBootstrapDone so we
            // retry on the next launch.
            Console.Error.WriteLine($"[SpamFilterEngine.Bootstrap] {ex.Message}");
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
