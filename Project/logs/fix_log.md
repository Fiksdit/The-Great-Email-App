# Fix Log

Permanent record of bug-class changes per rulebook §16. Newest first.

---

## FIX-2026-05-12-002 — Tray notifier crashed on first new-mail event, then Windows showed a "find an app in the Microsoft Store" popup

**Area:** Services / TrayNotifier · NewMailPoller balloon delivery
**Status:** ✅ Fixed
**Priority:** P1 (crashes the process on idle)

**Symptom**
- Within ~30s of launch the app crashed with no visible dialog of its own.
- A few seconds later Windows surfaced a "Look for an app in the Microsoft Store to open this link" popup — the .NET runtime's crash-recovery path falling through to the (unregistered) toast-activation URI handler.
- Repeated on every launch as long as the IMAP poller had any new mail to report.

**Replicate**
1. Launch the app on a machine where the next IMAP poll cycle will surface new mail (any unread arrives within the poll interval).
2. Wait ~30 seconds. The window closes, then the Store popup appears.
3. `%LOCALAPPDATA%\GreatEmailApp\crash.log` contains:
   ```
   System.InvalidOperationException: TrayIcon is not created.
     at H.NotifyIcon.Core.TrayIcon.EnsureCreated()
     at H.NotifyIcon.TaskbarIcon.ShowNotification(...)
     at GreatEmailApp.Services.TrayNotifier.ShowBalloon(...)
   ```

**Root cause**
- `TrayNotifier` instantiates `new TaskbarIcon()` from code and sets `ToolTipText` + `IconSource`, but never hosts the icon in a XAML tree and never calls `ForceCreate()`. H.NotifyIcon's WPF binding defers native tray-icon registration to either of those triggers.
- `_icon.ShowNotification(...)` calls `EnsureCreated()` internally — which throws when the native icon wasn't registered.
- The `DispatcherUnhandledException` hook in `App.OnStartup` deliberately re-raises (`Handled = false`) so the process dies, by design.
- After the crash, Windows' shell tries to deliver the queued toast activation through a `ms-notification:` / toast-callback channel that has no handler for this raw (non-MSIX, no AppUserModelID) EXE → it surfaces the "look in the Store" popup.

**Tried**
- Nothing — diagnosed in one pass by reading `crash.log` (three identical stacks across 11 minutes) plus the `H.NotifyIcon` source pointer in the trace.

**Fix**
- `TrayNotifier` calls `_icon.ForceCreate()` immediately after configuring the icon, so the native tray slot exists before any `ShowNotification` is attempted. Wrapped in try/catch with a `crash.log` append so a tray failure on shell-not-ready never crashes the app.
- `ShowBalloon` also wraps `_icon.ShowNotification` in try/catch — same rationale, belt and suspenders. Toast delivery is best-effort; per rulebook §11 we never crash the app over a missed balloon.

**Files changed**
- `src/GreatEmailApp/Services/TrayNotifier.cs` (Rev 2 → 3)

**Rulebook**
- §11 (Error Handling) — toast/balloon delivery is non-critical I/O; surfaces of the form "service unavailable" never propagate to a process-killing exception.
- §2 Surgical Change Rule — only TrayNotifier touched; the poller and unhandled-exception hook are unchanged.

**Session:** 2026-05-12
**Commit:** _pending_

---

## FIX-2026-05-12-001 — Reading pane stayed blank when switching emails in list view

**Area:** Controls / MessageBodyView · Reading pane body render
**Status:** ✅ Fixed
**Priority:** P1 (core reading flow)

**Symptom**
- Click a message in the middle pane → headers, avatar, subject, To: line all update — but the body area in the WebView2 stays blank (or shows the previously rendered body for a flash, then goes blank).
- Workaround discovered by users: click away to a different message and back; sometimes works on the third try. Inconsistent.

**Replicate**
1. Open Inbox, click message A. Body renders fine (first selection after launch).
2. Click message B. Headers swap to B. Body pane is blank.
3. Click message A again. Sometimes A's body comes back, sometimes blank.

**Root cause**
- `SelectMessageAsync` (MainViewModel.cs:328) sets `SelectedMessage = B` first → WPF re-binds `ReadingPane`'s inner DataContext → `MessageBodyView.Message` DP changes → `OnMessageChanged` fires → `Render()` runs **synchronously** reading `B.BodyHtml`/`B.BodyPlain`, both still empty at this point → WebView2 renders the empty wrapper doc.
- THEN `_imap.FetchBodyAsync(B, …)` returns ~200 ms later → assigns the strings on the model → calls `B.OnBodyLoaded()` which fires `PropertyChanged` for `BodyHtml`, `BodyPlain`, `BodyDisplay`.
- **Nothing in `MessageBodyView` listened for those events.** Its only render trigger was the `Message` DP changed callback — and the DP value (the `MessageViewModel` reference) was unchanged.
- `MainViewModel` tried to nudge things at line 361 with `OnPropertyChanged(nameof(SelectedMessage))`, but the binding's new value reference-equals the old, so WPF's DP system short-circuits and `OnMessageChanged` isn't re-invoked.

**Tried**
- Nothing — diagnosed on first read of `MessageBodyView.Render()` + `OnMessageChanged` + the body-fetch tail of `SelectMessageAsync`. The control's `Refresh()` method existed but was never called, which was the tell.

**Fix**
- `MessageBodyView` now tracks `_subscribedMessage` and subscribes to `INotifyPropertyChanged.PropertyChanged` on the bound `MessageViewModel`. When `BodyHtml`/`BodyPlain`/`BodyDisplay` fires, it re-calls `Render()`. Switching messages detaches the previous subscription first to prevent leaks.

**Files changed**
- `src/GreatEmailApp/Controls/MessageBodyView.xaml.cs` (Rev 1 → 2)

**Rulebook**
- §2 Surgical Change Rule — touched only the file with the missing wiring. `MainViewModel`'s leftover `OnPropertyChanged(SelectedMessage)` call is now redundant but harmless; left in place per §2.
- §10 (Components & UI/UX) → reinforces: DP-only re-render is a trap when the model behind the DP mutates async. Listen to `PropertyChanged` on the model when its inner state can change after the DP is set.

**Session:** 2026-05-12
**Commit:** _pending_

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
