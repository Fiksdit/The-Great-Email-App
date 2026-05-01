// FILE: src/GreatEmailApp.Core/Notifications/NewMailPoller.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Background poller: every Settings.SyncIntervalMinutes, walks each account's
// inbox and diffs the recent UID list against the per-account "last seen"
// snapshot. New UIDs fan out as NewMailEvent for the UI layer to surface.
//
// Why polling and not IMAP IDLE: IDLE keeps a connection open per account, is
// touchier across mobile-NAT / sleep / wake transitions, and would belong with
// a proper background sync engine in a future phase. Polling at 5-min cadence
// is good enough for "tell me when something arrives while I'm coding" and
// can be replaced wholesale without touching consumers.
//
// State persistence: notifications-state.json is local-only. Sync intentionally
// excludes it (rulebook §7B sibling — per-PC seen state shouldn't roam).

using System.Collections.Concurrent;
using System.Text.Json;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Notifications;

public interface INewMailPoller
{
    /// <summary>Fires once per detected new message, on a background thread.
    /// Subscribers must marshal to the UI thread themselves.</summary>
    event EventHandler<NewMailEvent>? NewMailDetected;

    void Start();
    void Stop();

    /// <summary>Force a poll right now (e.g. after the user clicks Send/Receive).</summary>
    Task PollOnceAsync(CancellationToken ct = default);
}

public sealed class NewMailPoller : INewMailPoller, IDisposable
{
    // Cap the per-account fetch so a brand-new install doesn't surface 10k
    // messages as "new". The first poll seeds the seen-set; we only notify on
    // UIDs that appear AFTER the seed.
    private const int RecentMessageLimit = 50;

    private readonly AppSettings _settings;
    private readonly IAccountStore _accountStore;
    private readonly ICredentialStore _creds;
    private readonly IImapService _imap;

    private readonly System.Timers.Timer _timer = new() { AutoReset = false };
    private readonly SemaphoreSlim _pollGate = new(1, 1);
    private readonly ConcurrentDictionary<string, HashSet<uint>> _seen = new();
    private bool _stateLoaded;
    private bool _running;

    public event EventHandler<NewMailEvent>? NewMailDetected;

    public NewMailPoller(
        AppSettings settings,
        IAccountStore accountStore,
        ICredentialStore creds,
        IImapService imap)
    {
        _settings = settings;
        _accountStore = accountStore;
        _creds = creds;
        _imap = imap;
        _timer.Elapsed += async (_, _) => await PollOnceAsync().ConfigureAwait(false);
    }

    public void Start()
    {
        _running = true;
        // Kick off an immediate poll, then the elapsed handler reschedules.
        _ = Task.Run(async () => await PollOnceAsync().ConfigureAwait(false));
    }

    public void Stop()
    {
        _running = false;
        _timer.Stop();
    }

    public async Task PollOnceAsync(CancellationToken ct = default)
    {
        if (!await _pollGate.WaitAsync(0, ct).ConfigureAwait(false)) return; // already polling
        try
        {
            EnsureStateLoaded();
            if (!_settings.EnableNewMailNotifications) { Reschedule(); return; }

            foreach (var account in _accountStore.LoadAll())
            {
                ct.ThrowIfCancellationRequested();
                await PollOneAsync(account, ct).ConfigureAwait(false);
            }
            SaveState();
        }
        catch (Exception ex)
        {
            // Polling is best-effort. Don't let a transient network glitch take
            // down the timer loop.
            Console.Error.WriteLine($"[NewMailPoller] {ex.Message}");
        }
        finally
        {
            _pollGate.Release();
            Reschedule();
        }
    }

    private async Task PollOneAsync(Account account, CancellationToken ct)
    {
        var creds = _creds.Read(account.Id);
        if (creds is null) return;

        var inboxPath = "INBOX"; // MailKit: case-insensitive but uppercase is canonical
        var result = await _imap.ListMessagesAsync(account, creds.Value.Password,
            inboxPath, RecentMessageLimit, ct).ConfigureAwait(false);
        if (result is not Result<List<Message>>.Ok ok) return;

        var seenSet = _seen.GetOrAdd(account.Id, _ => new HashSet<uint>());
        var firstSeed = seenSet.Count == 0;

        foreach (var msg in ok.Value)
        {
            if (!uint.TryParse(msg.Id, out var uid)) continue;
            if (!seenSet.Add(uid)) continue; // already known

            // First-ever poll for this account just seeds the set — don't blast
            // the user with 50 "new" notifications for what's just history.
            if (firstSeed) continue;

            // Skip messages flagged as already read on the server — those aren't
            // really new to the user, just new to this PC.
            if (!msg.Unread) continue;

            NewMailDetected?.Invoke(this, new NewMailEvent(account, msg));
        }

        // Trim seen-set so it doesn't grow forever. Keep the most recent ~500 UIDs.
        if (seenSet.Count > 500)
        {
            var trimmed = seenSet.OrderByDescending(u => u).Take(500).ToHashSet();
            _seen[account.Id] = trimmed;
        }
    }

    private void Reschedule()
    {
        if (!_running) return;
        var minutes = Math.Max(1, _settings.SyncIntervalMinutes);
        _timer.Interval = TimeSpan.FromMinutes(minutes).TotalMilliseconds;
        _timer.Start();
    }

    // ----- Persistence ---------------------------------------------------- //

    private void EnsureStateLoaded()
    {
        if (_stateLoaded) return;
        _stateLoaded = true;
        try
        {
            if (!File.Exists(AppPaths.NotificationsStateJson)) return;
            var json = File.ReadAllText(AppPaths.NotificationsStateJson);
            var dict = JsonSerializer.Deserialize<Dictionary<string, List<uint>>>(json);
            if (dict is null) return;
            foreach (var kv in dict)
                _seen[kv.Key] = new HashSet<uint>(kv.Value);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[NewMailPoller.LoadState] {ex.Message}");
        }
    }

    private void SaveState()
    {
        try
        {
            AppPaths.EnsureRoot();
            var dict = _seen.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
            var json = JsonSerializer.Serialize(dict);
            var tmp = AppPaths.NotificationsStateJson + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, AppPaths.NotificationsStateJson, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[NewMailPoller.SaveState] {ex.Message}");
        }
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
        _pollGate.Dispose();
    }
}
