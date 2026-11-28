// FILE: src/GreatEmailApp.Core/Rules/RulesEngine.cs
// Created: 2026-05-01 | Revised: 2026-11-27 | Rev: 4
// Changed by: Claude Opus 5.5 on behalf of James Reed
//
// Manual mail-rules engine. v1 — conditions on From/To/Subject/Body, actions
// MoveToFolder/MarkRead/Flag/Delete. Plays the rules in declaration order;
// StopOnMatch lets a rule short-circuit further evaluation per-message.
//
// Hookup: subscribes to NewMailPoller.MessagesPolled. The poller already does
// the IMAP fetch; we piggy-back on its message set so rules apply with no
// additional network round-trip per cycle. The "Run rules now" UI button
// calls ApplyAsync directly with whatever the user wants reprocessed.

using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Notifications;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Rules;

public sealed class RulesEngine : IRulesEngine
{
    private readonly IRulesStore _rules;
    private readonly IAccountStore _accounts;
    private readonly ICredentialStore _creds;
    private readonly IImapService _imap;
    private readonly INewMailPoller _poller;
    private bool _running;

    /// <summary>
    /// A "Mark priority" rule just marked a NEWLY ARRIVED message (never the
    /// re-scanned backlog or a manual run). The tray shows its own alert for
    /// these. Raised on a worker thread.
    /// </summary>
    public event EventHandler<PriorityMailEvent>? PriorityMailArrived;

    /// <summary>Raised after a run in which any Flag / Mark priority action succeeded.</summary>
    public event EventHandler? MarksApplied;

    private void RaisePriority(Account account, Message msg, MailRule rule)
    {
        try { PriorityMailArrived?.Invoke(this, new PriorityMailEvent(account, msg, rule.Name)); }
        catch { /* a UI handler must never break rule processing */ }
    }

    public RulesEngine(
        IRulesStore rules,
        IAccountStore accounts,
        ICredentialStore creds,
        IImapService imap,
        INewMailPoller poller)
    {
        _rules = rules;
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
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _poller.MessagesPolled -= OnMessagesPolled;
    }

    private async void OnMessagesPolled(object? sender, MessagesPolledEvent ev)
    {
        try { await ApplyAsync(ev.Account, ev.FolderPath, ev.Messages, newUids: ev.NewUids); }
        catch { /* best-effort; the next poll will try again */ }
    }

    // --------------------------------------------------------------------- //
    // Public surface
    // --------------------------------------------------------------------- //

    public async Task<Result<RuleApplyResult>> ApplyAsync(
        Account account, string folderPath, IEnumerable<Message> messages,
        CancellationToken ct = default, IReadOnlySet<uint>? newUids = null,
        Func<MailRule, bool>? ruleFilter = null)
    {
        var creds = _creds.Read(account.Id);
        if (creds is null)
            return Result.Fail<RuleApplyResult>($"No password stored for {account.EmailAddress}.");

        var rules = _rules.LoadAll()
            .Where(r => r.IsEnabled)
            .Where(r => string.IsNullOrEmpty(r.AccountId) || r.AccountId == account.Id)
            .Where(r => ruleFilter is null || ruleFilter(r))
            .ToList();
        if (rules.Count == 0)
            return Result.Ok(new RuleApplyResult(0, 0, 0, new List<string>()));

        var msgList = messages.ToList();
        int matched = 0, actionsRun = 0;
        bool anyMarked = false;
        var errors = new List<string>();

        foreach (var msg in msgList)
        {
            ct.ThrowIfCancellationRequested();
            if (!uint.TryParse(msg.Id, out var uid)) continue;

            // Walk rules in declaration order — first match still gets to fire
            // every action in its list; StopOnMatch only halts the OUTER loop.
            foreach (var rule in rules)
            {
                // Incoming-only rules touch just-arrived mail, never the
                // re-scanned backlog or a manual "Run rules now".
                if (rule.IncomingOnly && (newUids is null || !newUids.Contains(uid))) continue;
                if (!Evaluate(rule, msg)) continue;
                // Running rules over a folder that a rule files INTO (e.g. "Run
                // rules now" on Ads, with a rule "@walmart → Ads"): the mail is
                // already where it belongs — leave it alone rather than moving
                // it onto itself.
                if (rule.Actions.Any(a => a.Type == RuleAction.MoveToFolder
                        && string.Equals(a.Value?.Trim(), folderPath, StringComparison.OrdinalIgnoreCase)))
                {
                    if (rule.StopOnMatch) break;
                    continue;
                }
                matched++;
                foreach (var action in rule.Actions)
                {
                    var ran = await ExecuteAsync(account, creds.Value.Password, folderPath, uid,
                        msg, action, ct).ConfigureAwait(false);
                    if (ran is Result<bool>.Ok && action.IsPriority && newUids is not null && newUids.Contains(uid))
                        RaisePriority(account, msg, rule);
                    if (ran is Result<bool>.Ok) actionsRun++;
                    // Only new arrivals or a manual run — the per-poll re-scan of
                    // recent inbox mail would otherwise re-fire this every cycle.
                    if (ran is Result<bool>.Ok && action.Type == RuleAction.Flag
                        && (newUids is null || newUids.Contains(uid))) anyMarked = true;
                    else if (ran is Result<bool>.Fail f) errors.Add($"[{rule.Name}] {f.Error}");

                    // A move makes the original UID invalid for subsequent actions
                    // on the same message in this folder. Break out so we don't
                    // try to flag/delete a message that's no longer here.
                    if (action.Type == RuleAction.MoveToFolder || action.Type == RuleAction.Delete)
                        break;
                }
                if (rule.StopOnMatch) break;
            }
        }

        if (anyMarked)
        {
            try { MarksApplied?.Invoke(this, EventArgs.Empty); } catch { /* UI handler only */ }
        }
        return Result.Ok(new RuleApplyResult(msgList.Count, matched, actionsRun, errors));
    }

    // --------------------------------------------------------------------- //
    // Evaluation
    // --------------------------------------------------------------------- //

    private static bool Evaluate(MailRule rule, Message m)
    {
        if (rule.Conditions.Count == 0) return false;
        return rule.Match == RuleMatch.All
            ? rule.Conditions.All(c => MatchOne(c, m))
            : rule.Conditions.Any(c => MatchOne(c, m));
    }

    private static bool MatchOne(RuleCondition c, Message m)
    {
        var hay = c.Field switch
        {
            RuleField.From    => $"{m.Sender} {m.SenderEmail}",
            RuleField.To      => m.To ?? "",
            RuleField.Subject => m.Subject ?? "",
            RuleField.Body    => m.Preview ?? "",
            _ => "",
        };
        var needle = (c.Value ?? "").Trim();
        if (string.IsNullOrEmpty(needle)) return false;
        var cmp = StringComparison.OrdinalIgnoreCase;

        // "From ends with" takes a comma/semicolon/newline-separated list and
        // matches if the sender's address ends with ANY entry — e.g.
        // ".pro, .xyz, .top" blocks whole domain extensions in one condition.
        // The old whole-string check is kept so existing single-value rules
        // behave exactly as before. (Older clients see the list as one string,
        // match nothing, and skip the rule — safe degrade over sync.)
        if (c.Field == RuleField.From && c.Op == RuleOp.EndsWith)
        {
            if (hay.EndsWith(needle, cmp)) return true;
            var addr = (m.SenderEmail ?? "").Trim().TrimEnd('>');
            if (addr.Length == 0) return false;
            return SplitList(needle).Any(entry => addr.EndsWith(entry, cmp));
        }

        return c.Op switch
        {
            // Contains is smart: tokenize on whitespace (respecting "quoted phrases")
            // and require every token to appear in the haystack. So:
            //   "mobile sentrix"  → both words must appear, any order
            //   "james@fiksdit.com" → one token, plain substring
            //   "\"order shipped\""  → exact phrase
            RuleOp.Contains   => Tokenize(needle).All(t => hay.Contains(t, cmp)),
            RuleOp.Equals     => hay.Equals(needle, cmp),
            RuleOp.StartsWith => hay.StartsWith(needle, cmp),
            RuleOp.EndsWith   => hay.EndsWith(needle, cmp),
            _ => false,
        };
    }

    private static IEnumerable<string> SplitList(string s) =>
        s.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .Where(e => e.Length > 0);

    private static IEnumerable<string> Tokenize(string s)
    {
        // Whitespace tokenizer with double-quoted phrases as single tokens.
        var tokens = new List<string>();
        var buf = new System.Text.StringBuilder();
        bool inQuote = false;
        foreach (var ch in s)
        {
            if (ch == '"') { inQuote = !inQuote; continue; }
            if (!inQuote && char.IsWhiteSpace(ch))
            {
                if (buf.Length > 0) { tokens.Add(buf.ToString()); buf.Clear(); }
            }
            else buf.Append(ch);
        }
        if (buf.Length > 0) tokens.Add(buf.ToString());
        return tokens.Count == 0 ? new[] { s } : tokens;
    }

    // --------------------------------------------------------------------- //
    // Action execution
    // --------------------------------------------------------------------- //

    private async Task<Result<bool>> ExecuteAsync(
        Account account, string password, string folderPath, uint uid, Message msg,
        RuleActionItem action, CancellationToken ct)
    {
        switch (action.Type)
        {
            case RuleAction.MarkRead:
                return await _imap.SetSeenAsync(account, password, folderPath, uid, true, ct).ConfigureAwait(false);

            case RuleAction.Flag when action.IsPriority:
                return await _imap.SetMarkAsync(account, password, folderPath, uid, MailMark.Priority, true, ct).ConfigureAwait(false);

            case RuleAction.Flag:
                return await _imap.SetFlaggedAsync(account, password, folderPath, uid, true, ct).ConfigureAwait(false);

            case RuleAction.Delete when action.IsPermanentDelete:
                // Expunged in place — never appears in Trash. Irreversible.
                return await _imap.DeletePermanentlyAsync(account, password, folderPath, uid, ct).ConfigureAwait(false);

            case RuleAction.Delete:
                var del = await _imap.MoveToSpecialAsync(account, password, folderPath, uid,
                    SpecialFolder.Deleted, ct).ConfigureAwait(false);
                return del is Result<string>.Ok ? Result.Ok(true) : Result.Fail<bool>(del.AsError ?? "delete failed");

            case RuleAction.MoveToFolder:
                if (string.IsNullOrWhiteSpace(action.Value))
                    return Result.Fail<bool>("MoveToFolder action has no destination set.");
                return await _imap.MoveToFolderAsync(account, password, folderPath, uid, action.Value, ct).ConfigureAwait(false);

            default:
                return Result.Fail<bool>($"Unknown action: {action.Type}");
        }
    }
}
