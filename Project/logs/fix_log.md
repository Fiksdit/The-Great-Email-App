# Fix Log

Permanent record of bug-class changes per rulebook §16. Newest first.

---

## FIX-2026-05-10-002 — App crashed on first launch, ran fine on second (concurrent settings write + tray race)

**Area:** SyncCoordinator + JsonSettingsStore + TrayNotifier
**Status:** ✅ Fixed in v0.11.11
**Priority:** P1 — visible to every user on every cold start

**Symptom**
- Launch app → process exits silently within seconds.
- Reopen → fine.
- Manually close → next cold launch crashes again. Loop.

**Root cause — two independent crashes both fired during startup**

1. **Concurrent settings.json writes.** `App.RestoreAndStartSyncAsync()` ran two paths back-to-back:
   - `await Auth.TryRestoreAsync()` raised `SessionChanged` → kicked off `PullOrSeedAsync` on a worker thread.
   - Then `await SyncCoordinator.StartAsync()` called `PullOrSeedAsync` *again* because auth was now signed in.
   - Both `ApplyRemote` paths reached `JsonSettingsStore.Save()` simultaneously. They share the same `settings.json.tmp` filename, so `File.WriteAllText(tmp, …)` on the second one threw `IOException: file in use`. Unhandled → process death.

2. **Tray balloon before tray was realized.** `TrayNotifier.ShowBalloon` ran from a `MailPoller.NewMailDetected` dispatch. On first launch the H.NotifyIcon `Shell_NotifyIcon` registration hadn't completed yet, so `ShowNotification` threw `InvalidOperationException: TrayIcon is not created.` Unhandled → process death.

The reason "second launch is fine": the cloud snapshot was already up-to-date by then (the partial first-run had pushed local), and the poller had no fresh mail to balloon.

**Tried**
- Nothing — diagnosed straight from `crash.log` (which had captured both stack traces from prior runs).

**Fix**
- `SyncCoordinator`: added `SemaphoreSlim _gate (1,1)`. Public `PushAsync` / `PullAsync` / `PullOrSeedAsync` acquire it and delegate to private `*CoreAsync` variants. Internal callers (e.g. `PullCore` falling back to push when local prefers) call the Core variants directly to avoid re-entrant deadlock. Disposed in Dispose.
- `JsonSettingsStore.Save`: wrapped the `.tmp + Move` in `lock (_saveLock)` for belt-and-suspenders against any other concurrent caller.
- `TrayNotifier.ShowBalloon`: wrapped `ShowNotification` in `try/catch (InvalidOperationException) / catch (Exception)`. Notifications are best-effort.

**Files changed**
- `src/GreatEmailApp.Core/Sync/SyncCoordinator.cs` — Rev 5: gate + Core split.
- `src/GreatEmailApp.Core/Services/JsonSettingsStore.cs` — Rev 3: save lock.
- `src/GreatEmailApp/Services/TrayNotifier.cs` — Rev 3: swallow tray-not-created.
- `src/GreatEmailApp/GreatEmailApp.csproj` — version 0.11.11.

**Verified**
- Cold start: alive, no crash.log written.
- Kill + relaunch cycle: both runs alive, no crash.log written.

**Rulebook**
- No new rule needed; existing §16 applies. Decision-log candidate: "Any code path that runs both at app-startup and from an event handler that startup itself raises must be safe to run concurrently — prefer a coordinator-level gate over per-callsite ordering."

---

## FIX-2026-05-10-001 — Sync silently flipped pulls into pushes; new PCs got zero accounts

**Area:** Sync / SyncCoordinator + SyncMetadata
**Status:** ✅ Fixed in v0.11.5
**Priority:** P0 — silent data divergence between PCs

**Symptom**
- New PC signed into Firebase, expecting 3 accounts to pull down from cloud. Got nothing — only the 1 account already present locally stayed. Settings → Sync → "Sync now" reported success.
- On a PC that had successfully pulled once, every subsequent pull was actually pushing the local roster to the cloud, overwriting whatever the other PC had pushed.

**Root cause**
Two compounding bugs in the FIX-2026-04-30-002 guard:

1. `SyncMetadata.HasUnpushedLocalChanges()` used `threshold.AddSeconds(-2)` for filesystem-mtime slop. That widens "has unpushed changes" instead of narrowing it — any file mtime within 2s of LastSyncedAt counted as unpushed. Wrong direction.
2. `SyncCoordinator.ApplyRemote()` set `_meta.LastSyncedAt = remote.UpdatedAt` (the snapshot's earlier server-side timestamp). But `ApplyRemote` had just written `accounts.json`, so its mtime = "now", which is later than `remote.UpdatedAt`. Combined with bug #1, this made every just-pulled device permanently look "unpushed" → next pull triggered `ShouldPreferLocalOver` → push the stale local data instead of applying remote.

User's symptom: PC A had 3 accounts, PC B had 1. Both kept pushing their own list and ignoring remote. Whichever PC last synced won the cloud doc — so PC B's recent activity had clobbered PC A's roster.

**Tried**
- Nothing — diagnosed directly from `sync-meta.json` mtime vs `accounts.json` mtime on the user's machine: file mtime `15:27:50.000`, LastSyncedAt `15:27:50.523`. With `-2s` slop, file looks newer; bug confirmed by inspection.

**Fix**
- `SyncMetadata.HasUnpushedLocalChanges`: slop direction inverted — `threshold.AddSeconds(+2)`. Only files clearly newer than baseline count as unpushed.
- `SyncCoordinator.ApplyRemote`: `_meta.LastSyncedAt = DateTimeOffset.UtcNow` after the local Save calls complete, so the new baseline is strictly later than any file we just wrote.
- FIX-2026-04-30-002's empty-cloud guard (clause 2 of `ShouldPreferLocalOver`) is unchanged — that protection was independent of the timestamp comparison and still blocks empty cloud from clobbering local.

**Files changed**
- `src/GreatEmailApp.Core/Sync/SyncMetadata.cs` — Rev 2: slop direction.
- `src/GreatEmailApp.Core/Sync/SyncCoordinator.cs` — Rev 3: ApplyRemote baseline timestamp.
- `src/GreatEmailApp/GreatEmailApp.csproj` — version 0.11.5.

**Recovery for the user**
1. Install 0.11.5 on **both** PCs (built-in updater, or copy install dir).
2. On the PC that has the 3 accounts: Settings → Sync → Sync now (push).
3. On the other PC: Settings → Sync → Sync now (pull). Roster appears.

**Rulebook**
- No new rule needed; existing §16 applies. Decision-log entry in roadmap may want a one-liner that timestamp slop in sync guards must always be in the conservative direction (treat near-equal as in-sync).

---

## FIX-2026-05-07-001 — Avatar pretended a user was signed in when they weren't

**Area:** TitleBar / Avatar button
**Status:** ✅ Fixed
**Priority:** P2

**Symptom**
- Top-right avatar always showed "JR" with a tooltip of `coolman0804@outlook.com`, regardless of whether anyone was actually signed into Firebase. Clicking it showed a `MessageBox` reading "Signed in: coolman0804@outlook.com / Sync: on".
- Misleading: implied an active account where none existed. Worse, the only path to actually sign in was buried in Settings → Sync — the avatar gave no hint of that.

**Replicate**
1. Fresh install (no `auth.dat`), launch app.
2. Hover top-right avatar → tooltip "coolman0804@outlook.com".
3. Click avatar → popup claims you're signed in.

**Root cause**
- `MainViewModel.AccountInitial` and `AccountEmail` were hardcoded literals (`"JR"`, `"coolman0804@outlook.com"`) — leftover sample-data from Phase 1 that never got reconnected to the real `IAuthService` when Phase 4 auth landed.
- `TitleBar.AvatarButton_Click` showed a placeholder `MessageBox` with no signed-out branch.

**Tried**
- Nothing — fixed on first attempt. Root cause was visible in a 3-line grep for `coolman` in the source tree.

**Fix**
- `MainViewModel.AccountInitial` / `AccountEmail` / `IsSignedIn` now derive from `App.Auth.Current`. Signed-out → initial `?`, tooltip `"Sign in"`.
- Subscribed to `App.Auth.SessionChanged` in the VM ctor so the avatar repaints on sign-in / sign-out / silent restore (dispatched onto the UI thread).
- `TitleBar.AvatarButton_Click` now branches on `App.Auth?.IsSignedIn`: when signed-out, opens `SettingsDialog` directly on the **Sync** tab via `OpenOnTab("Sync")`. When signed-in, keeps the lightweight info popup (Phase-4 placeholder line removed).

**Files changed**
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` (Rev 2 → 3)
- `src/GreatEmailApp/Controls/TitleBar.xaml.cs` (Rev 2 → 3)

**Rulebook**
- §2 Surgical Change Rule — touched only the two responsible files, no XAML or auth-service changes.
- Spiritual sibling of FIX-2026-04-30-002: same "fake data masquerading as real" anti-pattern, different surface (avatar instead of message list).

**Session:** 2026-05-07
**Commit:** _pending_

---

## FIX-2026-04-30-002 — Auto-sync wiped local accounts on first 0.4.0 launch

**Symptom**
- After installing 0.4.0 (which added auto-pull on startup), user's `accounts.json` was rewritten to `[]` and the sidebar fell back to sample data.
- The cloud document had been seeded with an empty accounts array on the very first sign-in (before any real account was added). Every subsequent startup pulled that stale empty snapshot and clobbered local state.

**Tried**
- Nothing — caught on first user report. Root cause was clear from `accounts.json`'s mtime matching the SyncCoordinator's pull-on-start window.

**Fix**
- New `SyncMetadata` (`%LOCALAPPDATA%\GreatEmailApp\sync-meta.json`) tracks `LastSyncedAt`. Local-only, never pushed.
- `SyncCoordinator.ShouldPreferLocalOver(remote)` returns true when:
  1. Local files were modified after `LastSyncedAt` (unpushed edits exist), OR
  2. Local has accounts but remote does not, AND remote isn't strictly newer than `LastSyncedAt`.
- When that fires, the coordinator **pushes local instead of applying remote**.
- Removed sample-data fallback from `MainViewModel.LoadAccounts` so an empty roster is just an empty UI ("No accounts yet — click Add account") instead of fake data masquerading as real.

**Files changed**
- `src/GreatEmailApp.Core/Storage/AppPaths.cs` — `SyncMetaJson` path.
- `src/GreatEmailApp.Core/Sync/SyncMetadata.cs` — new.
- `src/GreatEmailApp.Core/Sync/SyncCoordinator.cs` — wires metadata into Push + Pull + PullOrSeed; `ShouldPreferLocalOver` decision; updates `_meta.LastSyncedAt` on every push and apply-pull.
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` — empty-state replaces SampleData fallback; dead `LoadSampleMessages` removed.
- `src/GreatEmailApp.Core/Sample/SampleData.cs` — deleted.

**Rulebook**
- Existing decision log entry "Last-write-wins for settings sync conflicts" still holds. The fix layers a "did we already see that write?" check on top: a remote with a timestamp older than our last sync is treated as stale relative to subsequent local edits.

---

## FIX-2026-04-30-001 — White-window on launch (WPF hardware rendering broken)

**Symptom**
- App launches and reaches `MainWindow.ContentRendered` cleanly, no exceptions.
- Window chrome + entire client area paint as **pure white**. Mouse cursor changes to a pointer over interactive zones (so the visual tree is hit-testing correctly), but nothing draws.
- Reproduced on a fresh Win11 box. Same code on the user's other PC renders correctly.

**Tried**
- Cleaned `bin/`, `obj/`, full rebuild — no change.
- Verified `Themes/Dark.xaml` loads (30 keys present in `Application.Current.Resources.MergedDictionaries[1]`).
- `TryFindResource("AppBackgroundBrush")` returns `#FF1F1F1F` — brushes are resolvable, the data is correct.
- Skipped `ThemeManager.Apply()` entirely, relying on App.xaml's parse-time merged dictionaries. Still pure white. So Apply() wasn't to blame.
- `RenderCapability.Tier >> 16` reports **Tier 2** (full hardware acceleration available) but the GPU pipeline produces a blank surface.

**Fix**
Force `RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly` at the very top of `App.OnStartup`, before `base.OnStartup(e)`. WPF's software rasterizer is plenty fast for an email-client UI and removes a whole class of GPU-driver-dependent rendering bugs.

**Files changed**
- `src/GreatEmailApp/App.xaml.cs` (Rev 7) — set `ProcessRenderMode` first thing in `OnStartup`.

**Rulebook**
- New §17 (Rendering) added: "WPF process render mode is SoftwareOnly. Don't change without testing on every supported PC."
- Preflight Step 00 updated with the symptom signature so it's recognized on the next fresh-PC bringup instead of being re-debugged.
