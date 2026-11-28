// FILE: src/GreatEmailApp.Core/Services/MarkedMailCache.cs
// Created: 2026-11-27 | Revised: 2026-11-27 | Rev: 1
// Changed by: Claude Opus 5.5 on behalf of James Reed
//
// Last known Flagged / Priority messages per account, so the sidebar's
// all-folders views open instantly instead of waiting 10–15 s for an IMAP
// search of every folder. Kept in memory and in marked-mail.json (local-only,
// never synced). The server stays the source of truth: RefreshAsync re-runs
// the full search in the background (startup, after rules mark mail, on a
// timer) and replaces an account's list; local actions (flag, unflag, move,
// delete) patch it immediately in between.

using System.Text.Json;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Services;

public sealed class MarkedMailCache
{
    private readonly IImapService _imap;
    private readonly IAccountStore _accounts;
    private readonly ICredentialStore _creds;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    // key: accountId|mark → messages (bodies stripped)
    private Dictionary<string, List<Message>> _lists = new();
    // key: accountId|mark → error from that account's last refresh ("" = ok)
    private readonly Dictionary<string, string> _errors = new();
    // Local edits, replayed over a refresh that started before them so a
    // search already in flight can't undo a flag the user just set.
    private readonly List<(DateTimeOffset At, Message Msg, MailMark? Mark, bool On)> _edits = new();

    /// <summary>Raised (any thread) after a background refresh changed a list.</summary>
    public event EventHandler<MailMark>? Refreshed;

    public DateTimeOffset? LastRefreshed { get; private set; }

    public MarkedMailCache(IImapService imap, IAccountStore accounts, ICredentialStore creds)
    {
        _imap = imap;
        _accounts = accounts;
        _creds = creds;
        Load();
    }

    private static string Key(string accountId, MailMark mark) => accountId + "|" + mark;

    /// <summary>True once this mark has a stored list for at least one account.</summary>
    public bool HasData(MailMark mark)
    {
        lock (_lock) return _lists.Keys.Any(k => k.EndsWith("|" + mark, StringComparison.Ordinal));
    }

    /// <summary>All cached messages with this mark, every account, newest first (copies).</summary>
    public List<Message> Get(MailMark mark)
    {
        lock (_lock)
            return _lists.Where(kv => kv.Key.EndsWith("|" + mark, StringComparison.Ordinal))
                .SelectMany(kv => kv.Value)
                .OrderByDescending(m => m.SentAt ?? DateTimeOffset.MinValue)
                .Select(Clone)
                .ToList();
    }

    /// <summary>Problems from the last refresh, e.g. an account that couldn't be searched.</summary>
    public string Problems(MailMark mark)
    {
        lock (_lock)
            return string.Join(" ", _errors.Where(kv => kv.Key.EndsWith("|" + mark, StringComparison.Ordinal)
                && kv.Value.Length > 0).Select(kv => kv.Value));
    }

    /// <summary>
    /// Re-run the server search for one mark across all accounts with a saved
    /// password, replace the cached lists, save, and raise <see cref="Refreshed"/>.
    /// One refresh at a time; a call that arrives during one waits for it and
    /// then runs (so its result is never older than the request).
    /// </summary>
    public async Task RefreshAsync(MailMark mark, CancellationToken ct = default)
    {
        await _refreshGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var started = DateTimeOffset.UtcNow;
            var accounts = _accounts.LoadAll();
            var work = accounts.Select(async a =>
            {
                var c = _creds.Read(a.Id);
                if (c is null) return (a, (Result<List<Message>>?)null);
                return (a, (Result<List<Message>>?)await _imap.SearchMarkedAsync(a, c.Value.Password, mark, 500, ct)
                    .ConfigureAwait(false));
            });
            var results = await Task.WhenAll(work).ConfigureAwait(false);

            lock (_lock)
            {
                foreach (var (a, res) in results)
                {
                    var k = Key(a.Id, mark);
                    switch (res)
                    {
                        case null:
                            _errors[k] = $"No saved password on this PC for {a.EmailAddress}.";
                            break;
                        case Result<List<Message>>.Ok ok:
                            _lists[k] = ok.Value.Select(Strip).ToList();
                            _errors[k] = "";
                            break;
                        case Result<List<Message>>.Fail f:
                            // Keep the last good list; just report the problem.
                            _errors[k] = $"{a.EmailAddress} couldn't be searched ({f.Error}).";
                            break;
                    }
                }
                // Drop lists for accounts that no longer exist.
                var ids = accounts.Select(a => a.Id).ToHashSet();
                foreach (var k in _lists.Keys.Where(k => !ids.Contains(k.Split('|')[0])).ToList())
                    _lists.Remove(k);
                // Re-apply edits made since this search started; forget older ones.
                _edits.RemoveAll(e => e.At < started.AddMinutes(-5));
                // (10 s margin: the server write for an edit may still have been
                // in flight when the search began.)
                foreach (var e in _edits.Where(e => e.At >= started.AddSeconds(-10)))
                    ApplyUnlocked(e.Msg, e.Mark, e.On);
                LastRefreshed = DateTimeOffset.UtcNow;
            }
            Save();
        }
        finally { _refreshGate.Release(); }
        Refreshed?.Invoke(this, mark);
    }

    public Task RefreshAllAsync(CancellationToken ct = default) =>
        Task.WhenAll(RefreshAsync(MailMark.Priority, ct), RefreshAsync(MailMark.Flagged, ct));

    private Timer? _debounce;
    private Timer? _periodic;

    /// <summary>
    /// Refresh both lists after <paramref name="delay"/>. Calls inside that window
    /// collapse into one refresh (a burst of rule hits = one search).
    /// </summary>
    public void ScheduleRefresh(TimeSpan delay)
    {
        lock (_lock)
        {
            _debounce?.Dispose();
            _debounce = new Timer(_ => _ = RefreshAllSafeAsync(), null, delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Startup refresh after <paramref name="firstDelay"/>, then every <paramref name="every"/>.
    /// Catches marks changed elsewhere (Outlook, webmail, the other PC).</summary>
    public void StartBackgroundRefresh(TimeSpan firstDelay, TimeSpan every)
    {
        lock (_lock)
        {
            _periodic?.Dispose();
            _periodic = new Timer(_ => _ = RefreshAllSafeAsync(), null, firstDelay, every);
        }
    }

    private async Task RefreshAllSafeAsync()
    {
        try { await RefreshAllAsync().ConfigureAwait(false); }
        catch (Exception ex) { Console.Error.WriteLine($"[MarkedMailCache.Refresh] {ex.Message}"); }
    }

    /// <summary>A message was (un)marked here — patch the list now.</summary>
    public void SetMarked(Message m, MailMark mark, bool on)
    {
        lock (_lock)
        {
            _edits.Add((DateTimeOffset.UtcNow, Strip(m), mark, on));
            ApplyUnlocked(m, mark, on);
        }
        Save();
    }

    /// <summary>A message left its folder (moved, deleted) — drop it from every list.</summary>
    public void Remove(Message m)
    {
        lock (_lock)
        {
            _edits.Add((DateTimeOffset.UtcNow, Strip(m), null, false));
            ApplyUnlocked(m, null, false);
        }
        Save();
    }

    // mark null = message gone from its folder: remove from every list.
    private void ApplyUnlocked(Message m, MailMark? mark, bool on)
    {
        if (mark is not { } mk)
        {
            foreach (var list in _lists.Values) list.RemoveAll(x => Same(x, m));
            return;
        }
        var k = Key(m.AccountId, mk);
        if (!_lists.TryGetValue(k, out var l)) _lists[k] = l = new();
        l.RemoveAll(x => Same(x, m));
        if (on) l.Add(Strip(m));
    }

    private static bool Same(Message a, Message b) =>
        a.AccountId == b.AccountId && a.FolderId == b.FolderId && a.Id == b.Id;

    // Bodies aren't needed for the list and would bloat the file.
    private static Message Strip(Message m)
    {
        var c = Clone(m);
        c.BodyHtml = "";
        c.BodyPlain = "";
        return c;
    }

    private static Message Clone(Message m) => new()
    {
        Id = m.Id, AccountId = m.AccountId, FolderId = m.FolderId, Group = m.Group,
        Sender = m.Sender, SenderEmail = m.SenderEmail, Avatar = m.Avatar, Color = m.Color,
        Subject = m.Subject, Preview = m.Preview, Time = m.Time, FullTime = m.FullTime,
        SentAt = m.SentAt, Unread = m.Unread, Flagged = m.Flagged, Priority = m.Priority,
        Important = m.Important, To = m.To, Cc = m.Cc,
        Attachments = m.Attachments.ToList(),
        BodyHtml = m.BodyHtml, BodyPlain = m.BodyPlain,
    };

    // --------------------------------------------------------------------- //
    // Persistence
    // --------------------------------------------------------------------- //

    private void Load()
    {
        try
        {
            if (!File.Exists(AppPaths.MarkedMailJson)) return;
            var data = JsonSerializer.Deserialize<Dictionary<string, List<Message>>>(
                File.ReadAllText(AppPaths.MarkedMailJson));
            if (data is not null) lock (_lock) _lists = data;
        }
        catch (Exception ex)
        {
            // Corrupt cache → start empty; the background refresh rebuilds it.
            Console.Error.WriteLine($"[MarkedMailCache.Load] {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            string json;
            lock (_lock) json = JsonSerializer.Serialize(_lists);
            AppPaths.EnsureRoot();
            var tmp = AppPaths.MarkedMailJson + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, AppPaths.MarkedMailJson, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MarkedMailCache.Save] {ex.Message}");
        }
    }
}
