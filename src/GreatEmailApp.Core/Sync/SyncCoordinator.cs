// FILE: src/GreatEmailApp.Core/Sync/SyncCoordinator.cs
// Created: 2026-04-30 | Revised: 2026-05-10 | Rev: 5
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Glue between local saves, sign-in events, window focus, and Firestore.
// Sits above the stores + IFirestoreSyncService and lets the rest of the app
// stay sync-ignorant — anyone who edits AppSettings or accounts gets a push
// for free, just by going through the existing Save() paths.
//
// Responsibilities:
//   - Auto-PUSH on local save (debounced, suppressed during pull-apply)
//   - Auto-PULL on sign-in / app start (with seed if remote is empty)
//   - Auto-PULL on window-activated (cooldown so alt-tab doesn't spam)
//   - Manual TriggerPushAsync / TriggerPullAsync still available for the
//     "Sync now" button + tests.
//
// What this class is NOT:
//   - A real-time listener. Firestore REST has no streaming; that needs gRPC,
//     deferred indefinitely. Pull-on-focus is the practical equivalent.
//   - A conflict resolver. Last-write-wins per the roadmap decision log.

using GreatEmailApp.Core.Auth;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Sync;

public enum SyncEventKind { Idle, Pulling, Pushing, Applied, Pushed, Failed }

public sealed record SyncEvent(SyncEventKind Kind, string? Detail = null, DateTimeOffset? RemoteUpdatedAt = null);

public sealed class SyncCoordinator : IDisposable
{
    private readonly AppSettings _settings;
    private readonly ISettingsStore _settingsStore;
    private readonly IAccountStore _accountStore;
    private readonly IContactsStore _contactsStore;
    private readonly IRulesStore _rulesStore;
    private readonly IAuthService _auth;
    private readonly IFirestoreSyncService _sync;

    private readonly System.Timers.Timer _pushDebounce;
    private bool _suppressPush;
    private DateTimeOffset _lastPullAt = DateTimeOffset.MinValue;
    private readonly TimeSpan _activatePullCooldown = TimeSpan.FromSeconds(30);
    private SyncMetadata _meta = SyncMetadata.Load();
    // Serializes all push/pull operations. App startup can fire two concurrent
    // PullOrSeedAsync paths (SessionChanged from TryRestore + StartAsync after
    // it returns) which previously raced on settings.json.tmp and crashed.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public event EventHandler<SyncEvent>? StateChanged;

    /// <summary>
    /// Fired after a remote pull rewrote local state. UI layers (sidebar, settings
    /// dialog, theme manager) should refresh from <see cref="AppSettings"/> and
    /// the account store — both have new values in place.
    /// </summary>
    public event EventHandler? RemotePullApplied;

    public SyncCoordinator(
        AppSettings settings,
        ISettingsStore settingsStore,
        IAccountStore accountStore,
        IContactsStore contactsStore,
        IRulesStore rulesStore,
        IAuthService auth,
        IFirestoreSyncService sync)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _accountStore = accountStore;
        _contactsStore = contactsStore;
        _rulesStore = rulesStore;
        _auth = auth;
        _sync = sync;

        _pushDebounce = new System.Timers.Timer(3000) { AutoReset = false };
        _pushDebounce.Elapsed += async (_, _) => await PushNowAsync().ConfigureAwait(false);

        _settingsStore.Saved += OnLocalSaved;
        _accountStore.Saved  += OnLocalSaved;
        _contactsStore.Saved += OnLocalSaved;
        _rulesStore.Saved    += OnLocalSaved;
        _auth.SessionChanged += OnSessionChanged;
    }

    // --------------------------------------------------------------------- //
    // Public surface
    // --------------------------------------------------------------------- //

    /// <summary>
    /// Call this from App.OnStartup once auth restoration has had a chance to
    /// run. If we're already signed in we'll do an initial pull; otherwise
    /// nothing happens until the user signs in.
    /// </summary>
    public async Task StartAsync()
    {
        if (_auth.IsSignedIn) await PullOrSeedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Hook this to <c>Window.Activated</c>. Cheap pull when the user comes
    /// back to the app — covers "I changed something on the other PC".
    /// </summary>
    public void OnWindowActivated()
    {
        if (!_auth.IsSignedIn) return;
        if (DateTimeOffset.UtcNow - _lastPullAt < _activatePullCooldown) return;
        _ = PullAsync();
    }

    /// <summary>Force an immediate push now (manual "Sync now" button).</summary>
    public Task PushNowAsync() => PushAsync();

    /// <summary>Force an immediate pull now.</summary>
    public Task PullNowAsync() => PullAsync();

    public void Dispose()
    {
        _settingsStore.Saved -= OnLocalSaved;
        _accountStore.Saved  -= OnLocalSaved;
        _contactsStore.Saved -= OnLocalSaved;
        _rulesStore.Saved    -= OnLocalSaved;
        _auth.SessionChanged -= OnSessionChanged;
        _pushDebounce.Dispose();
        _gate.Dispose();
    }

    // --------------------------------------------------------------------- //
    // Event handlers
    // --------------------------------------------------------------------- //

    private void OnLocalSaved(object? sender, EventArgs e)
    {
        if (_suppressPush) return;
        if (!_auth.IsSignedIn) return;
        // Restart the debounce window on every save in the burst.
        _pushDebounce.Stop();
        _pushDebounce.Start();
    }

    private async void OnSessionChanged(object? sender, EventArgs e)
    {
        if (_auth.IsSignedIn)
        {
            await PullOrSeedAsync().ConfigureAwait(false);
        }
    }

    // --------------------------------------------------------------------- //
    // Push / pull internals
    // --------------------------------------------------------------------- //

    private async Task PushAsync()
    {
        if (!_auth.IsSignedIn) return;
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await PushCoreAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task PushCoreAsync()
    {
        if (!_auth.IsSignedIn) return;
        StateChanged?.Invoke(this, new SyncEvent(SyncEventKind.Pushing));

        var pushedAt = DateTimeOffset.UtcNow;
        var snapshot = new SyncSnapshot(
            _settings,
            _accountStore.LoadAll().ToList(),
            pushedAt,
            _contactsStore.LoadAll().ToList(),
            _rulesStore.LoadAll().ToList());

        var result = await _sync.PushAsync(snapshot).ConfigureAwait(false);
        if (result is Result<bool>.Ok)
        {
            _meta.LastSyncedAt = pushedAt;
            _meta.Save();
            StateChanged?.Invoke(this, new SyncEvent(SyncEventKind.Pushed));
        }
        else
        {
            StateChanged?.Invoke(this, new SyncEvent(SyncEventKind.Failed, ((Result<bool>.Fail)result).Error));
        }
    }

    private async Task PullAsync()
    {
        if (!_auth.IsSignedIn) return;
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await PullCoreAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task PullCoreAsync()
    {
        if (!_auth.IsSignedIn) return;
        _lastPullAt = DateTimeOffset.UtcNow;
        StateChanged?.Invoke(this, new SyncEvent(SyncEventKind.Pulling));

        var pull = await _sync.PullAsync().ConfigureAwait(false);
        if (pull is Result<SyncSnapshot?>.Fail f)
        {
            StateChanged?.Invoke(this, new SyncEvent(SyncEventKind.Failed, f.Error));
            return;
        }
        var remote = ((Result<SyncSnapshot?>.Ok)pull).Value;
        if (remote is null) return; // No remote yet — nothing to apply.

        if (ShouldPreferLocalOver(remote))
        {
            // Local has unpushed edits newer than what's on the cloud. Push, don't apply.
            // Already inside the gate — call the core push to avoid re-entrant lock.
            await PushCoreAsync().ConfigureAwait(false);
            return;
        }

        if (IsAlreadyApplied(remote))
        {
            // Same snapshot we last applied — nothing to do. Avoids rebuilding
            // the UI tree on the periodic window-activated pull, which would
            // clear the user's folder selection and message preview.
            return;
        }

        ApplyRemote(remote);
        StateChanged?.Invoke(this, new SyncEvent(SyncEventKind.Applied, RemoteUpdatedAt: remote.UpdatedAt));
    }

    private async Task PullOrSeedAsync()
    {
        if (!_auth.IsSignedIn) return;
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await PullOrSeedCoreAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task PullOrSeedCoreAsync()
    {
        if (!_auth.IsSignedIn) return;
        _lastPullAt = DateTimeOffset.UtcNow;
        StateChanged?.Invoke(this, new SyncEvent(SyncEventKind.Pulling));

        var pull = await _sync.PullAsync().ConfigureAwait(false);
        if (pull is Result<SyncSnapshot?>.Fail f)
        {
            StateChanged?.Invoke(this, new SyncEvent(SyncEventKind.Failed, f.Error));
            return;
        }
        var remote = ((Result<SyncSnapshot?>.Ok)pull).Value;

        if (remote is null)
        {
            // First device — seed the cloud with whatever we have locally.
            await PushCoreAsync().ConfigureAwait(false);
            return;
        }

        if (ShouldPreferLocalOver(remote))
        {
            // Cloud has stale state, local has unpushed edits — protect them.
            await PushCoreAsync().ConfigureAwait(false);
            return;
        }

        if (IsAlreadyApplied(remote))
        {
            return;
        }

        ApplyRemote(remote);
        StateChanged?.Invoke(this, new SyncEvent(SyncEventKind.Applied, RemoteUpdatedAt: remote.UpdatedAt));
    }

    /// <summary>
    /// True when the remote snapshot is the same one (or older than the one)
    /// we've already adopted locally — i.e. nothing has been pushed by another
    /// device since our last successful sync.
    /// </summary>
    private bool IsAlreadyApplied(SyncSnapshot remote)
    {
        if (_meta.LastSyncedAt is null) return false;
        // Remote stamp older than or equal to our baseline → already seen.
        return remote.UpdatedAt <= _meta.LastSyncedAt.Value;
    }

    /// <summary>
    /// Returns true when the cloud snapshot should be ignored in favor of pushing
    /// our local state. Triggered by FIX-2026-04-30-002: a stale empty cloud
    /// snapshot was overwriting a non-empty local accounts roster on every startup.
    /// </summary>
    private bool ShouldPreferLocalOver(SyncSnapshot remote)
    {
        // 1. Local has writes newer than the last successful sync → unpushed edits.
        if (_meta.HasUnpushedLocalChanges()) return true;

        // 2. Belt-and-suspenders: never replace a non-empty local account roster
        //    with an empty cloud one unless the cloud is *clearly* fresher than
        //    our last sync. "Clearly fresher" = strictly newer than LastSyncedAt.
        var localCount = _accountStore.LoadAll().Count;
        if (localCount > 0 && remote.Accounts.Count == 0)
        {
            if (_meta.LastSyncedAt is null) return true;
            if (remote.UpdatedAt <= _meta.LastSyncedAt.Value) return true;
        }
        return false;
    }

    private void ApplyRemote(SyncSnapshot remote)
    {
        // Suppress the push that would otherwise fire from our own Save() calls
        // below — we just *received* this state, pushing it back is a thrash loop.
        _suppressPush = true;
        try
        {
            // Mutate the live AppSettings instance in place so anyone holding a
            // reference (Theme.Apply, view models bound to App.Settings) sees
            // the new values without having to swap the object.
            _settings.Theme               = remote.Settings.Theme;
            _settings.Accent              = remote.Settings.Accent;
            _settings.Ribbon              = remote.Settings.Ribbon;
            _settings.Density             = remote.Settings.Density;
            _settings.SidebarWidth        = remote.Settings.SidebarWidth;
            _settings.MailListWidth       = remote.Settings.MailListWidth;
            _settings.Zoom                = remote.Settings.Zoom;
            _settings.ShowHtml            = remote.Settings.ShowHtml;
            _settings.AllowRemoteImages   = remote.Settings.AllowRemoteImages;
            _settings.MarkReadDelaySeconds = remote.Settings.MarkReadDelaySeconds;
            _settings.SyncIntervalMinutes = remote.Settings.SyncIntervalMinutes;
            _settingsStore.Save(_settings);
            _accountStore.Save(remote.Accounts);
            if (remote.Contacts is not null) _contactsStore.Save(remote.Contacts);
            if (remote.Rules    is not null) _rulesStore.Save(remote.Rules);
        }
        finally { _suppressPush = false; }

        // Baseline must be AFTER the local writes above, not the remote
        // snapshot's earlier UpdatedAt — otherwise the just-written files'
        // mtimes will look "newer than last sync" and HasUnpushedLocalChanges
        // returns true on every subsequent pull, flipping legitimate pulls
        // into stale pushes (FIX-2026-05-10-001).
        _meta.LastSyncedAt = DateTimeOffset.UtcNow;
        _meta.Save();

        RemotePullApplied?.Invoke(this, EventArgs.Empty);
    }
}
