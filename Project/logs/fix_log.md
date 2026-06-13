# Fix Log

Permanent record of bug-class changes per rulebook §16. Newest first.

---

## FIX-2026-06-12-001 — Removed built-in spam keyword reappeared on next launch

**Area:** Core / JsonSpamConfigStore + SpamFilterConfig · default-keyword merge
**Status:** ✅ Fixed
**Priority:** P2 (spam keyword editor couldn't permanently remove a default)

**Symptom**
- Settings → Spam → remove a built-in keyword (e.g. "ebitda"), click Done. It's gone from the list. Reopen Settings (or wait for the next poll) and the keyword is back.

**Replicate**
1. Settings → Spam, remove any keyword that ships as a default.
2. Close and reopen the dialog → the keyword has returned.

**Root cause**
- `JsonSpamConfigStore.Load` merges every `SpamFilterConfig.BuiltInKeywords` entry missing from the user's file, so a shipped keyword expansion reaches existing installs. But the merge had no notion of an *intentional* removal — a deleted default was simply "missing," so it got re-added on the very next load. This was a documented Phase-1 limitation (the store's own header earmarked a "RemovedDefaults" list for Phase 2); the spam-keyword editor (T3, this sprint) made it user-reachable, so it had to be closed.

**Tried**
- Nothing — the limitation was already diagnosed and documented in the store's header comment. Implemented the earmarked fix directly.

**Fix**
- `SpamFilterConfig`: added `RemovedDefaults` (List<string>) — built-ins the user explicitly removed.
- `JsonSpamConfigStore.Load`: the default-merge now skips any built-in present in `RemovedDefaults`, so removals stick.
- `SettingsViewModel.SaveSpamConfig`: recomputes `RemovedDefaults` = built-ins not currently in the keyword list on every save, so removing re-adds to the set and re-adding (or "Restore default keywords") clears it.
- Rides the existing `spam_json` Firestore sync, so removals propagate across PCs.

**Files changed**
- `src/GreatEmailApp.Core/Spam/SpamFilterConfig.cs` (Rev 4 → 5)
- `src/GreatEmailApp.Core/Services/JsonSpamConfigStore.cs` (Rev 2 → 3)
- `src/GreatEmailApp/ViewModels/SettingsViewModel.cs` (Rev 6)

**Rulebook**
- §2 Surgical Change Rule — merge logic + one VM method; classifier untouched.
- Completes the Phase-2 follow-up the Phase-1 store header explicitly deferred.

**Session:** 2026-06-12
**Commit:** _pending_

---

## FIX-2026-05-15-001 — New-mail notification toggle silently disabled the entire polling subsystem

**Area:** Notifications / NewMailPoller + TrayNotifier · poll cycle gating
**Status:** ✅ Fixed in v0.12.5
**Priority:** P1 (turning off one checkbox bricked search index, spam filter, rules, and auto-refresh)

**Symptom**
- Settings → Notifications → "New-mail notifications" off → balloons stop (expected) **and** the search indexer, spam filter, rules engine, and mail-list auto-refresh all go dormant (not expected). The only visible symptom was "no balloons"; the actual blast radius was the whole background subsystem.

**Replicate**
1. Turn off new-mail notifications in Settings.
2. Receive new mail. No cache update, no spam routing, no rules, no auto-refresh — nothing fires until the toggle is turned back on.

**Root cause**
- `NewMailPoller.PollOnceAsync` had an early return gated on `App.Settings.EnableNewMailNotifications`. With it false, the poll bailed before raising `MessagesPolled` / `NewMailDetected`, so every downstream subscriber (cache writer, `RulesEngine`, `SpamFilterEngine`, `MainViewModel` auto-refresh) was starved of events. The toggle was wired to the wrong layer — it gated the *poll*, not the *balloon*.
- Side effect: the Phase-1 spam filter built earlier the same session had genuinely never run on incoming mail, because the poller bailed before `SpamFilterEngine`'s `MessagesPolled` handler could fire.

**Tried**
- Nothing — diagnosed on first read of `PollOnceAsync`. The early-return-on-a-notification-flag was the obvious mismatch against the design intent "app open implies polling on."

**Fix**
- `NewMailPoller`: dropped the early return. Polling now always runs while the app is open; subscribers get events on every cycle.
- `TrayNotifier.OnNewMail`: now gates on `App.Settings.EnableNewMailNotifications` before buffering the event, so balloon behavior is unchanged from the user's perspective — the gate just moved to the correct (balloon-only) layer.

**Files changed**
- `src/GreatEmailApp.Core/Notifications/NewMailPoller.cs` — removed poll-gating early return.
- `src/GreatEmailApp/Services/TrayNotifier.cs` — gate balloon on the notification setting.

**Rulebook**
- §2 Surgical Change Rule — only the two responsible files touched; subscriber wiring left alone.
- Decision-log candidate: "A user-facing toggle must gate the narrowest behavior it names. A 'notifications' switch controls notifications, never the data pipeline they observe."

**Session:** 2026-05-15
**Commit:** 70e024f

---

## FIX-2026-05-15-002 — Mail list briefly showed weeks-old ("May 6") mail because the cache sorted display strings, not dates

**Area:** Search / SqliteMessageCache · envelope sort order
**Status:** ✅ Fixed in v0.12.5
**Priority:** P1 (wrong mail on top every time a folder repaints from cache)

**Symptom**
- Reopen the Inbox and, for a flash, weeks-old messages (e.g. dated "May 6") sit above today's mail before the live IMAP fetch corrects it. The cache-paint's top 25 reliably returned Tuesday-dated mail ahead of today's.

**Replicate**
1. Open a folder (paints from cache instantly).
2. Watch the first ~25 rows before the IMAP round-trip completes — order is non-chronological.

**Root cause**
- `SqliteMessageCache` stored `sent_at` as the human-display `FullTime` string (`"Wed, May 13, 2026, 3:22 PM"`), then `SELECT ... ORDER BY sent_at DESC` sorted **lexicographically**. The day-of-week prefix dominated: "Wed" > "Tue" > "Thu" > … — completely independent of the actual date.

**Tried**
- Nothing — diagnosed on first read. The `ORDER BY sent_at DESC` over a column holding `"Wed, May 13…"` strings was self-evidently a lexicographic-vs-chronological bug.

**Fix**
- Added `Message.SentAt` (`DateTimeOffset?`) sourced from MailKit's envelope `Date`.
- `UpsertEnvelopesAsync` now writes ISO 8601 UTC to `sent_at`; `GetEnvelopesAsync` reads it back, repopulates `SentAt` + display strings, and filters its `SELECT` to ISO rows only (`WHERE sent_at LIKE '____-__-__T%'`) so legacy display-format rows are skipped until the next poll rewrites them. **No migration needed — the cache self-heals** on the next poll.

**Files changed**
- `src/GreatEmailApp.Core/Models/Message.cs` — `SentAt` property.
- `src/GreatEmailApp.Core/Search/SqliteMessageCache.cs` — ISO write/read + ISO-only SELECT filter.
- `src/GreatEmailApp.Core/Services/ImapService.cs` — populate `SentAt` from envelope `Date`.

**Rulebook**
- §2 Surgical Change Rule — cache + model only; UI untouched.
- §14 — any ordered list query must sort on a sortable key, never a localized display string.

**Session:** 2026-05-15
**Commit:** 0e81369

---

## FIX-2026-05-15-003 — Mail list didn't reset to the top when switching folders

**Area:** Controls / MailList · ScrollViewer offset on folder change
**Status:** ✅ Fixed in v0.12.5
**Priority:** P2 (navigation annoyance)

**Symptom**
- Click a folder and the list keeps the scroll position from the folder you just left, instead of showing the newest message at the top.

**Replicate**
1. Scroll halfway down folder A.
2. Click folder B → list opens mid-scroll rather than at the top.

**Root cause**
- The `ScrollViewer` wrapping the mail-list `ItemsControl` had no signal to reset its vertical offset when the `Messages` collection was rebuilt via Clear + Add. WPF preserved the prior offset across the rebuild.

**Tried**
- Nothing — diagnosed alongside FIX-2026-05-15-002 (same commit); the missing scroll-reset on collection rebuild was clear from the control wiring.

**Fix**
- `MainViewModel` raises `FolderLoaded` after `SelectFolderAsync` finishes its live IMAP fetch. `MailList.xaml.cs` subscribes via `DataContextChanged` and calls `MessageScroll.ScrollToTop()` on the named `ScrollViewer`.
- Deliberately **not** raised from `RefreshCurrentFolderAsync` (auto-refresh on poll) — preserving the user's scroll position is correct there; a background poll shouldn't yank them away from what they're reading.

**Files changed**
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` — `FolderLoaded` event after folder fetch.
- `src/GreatEmailApp/Controls/MailList.xaml` + `MailList.xaml.cs` — named `ScrollViewer` + `ScrollToTop()` on `FolderLoaded`.

**Rulebook**
- §2 Surgical Change Rule — scoped to folder-switch path; refresh path intentionally excluded.

**Session:** 2026-05-15
**Commit:** 0e81369

---

## FIX-2026-05-15-004 — Reading pane went blank on the selected message after a background auto-refresh

**Area:** ViewModels / MainViewModel + Controls / MessageBodyView · body preservation across refresh
**Status:** ✅ Fixed in v0.12.5
**Priority:** P1 (current message blanks out while reading)

**Symptom**
- While reading a message, the 5-minute poll fires the new auto-refresh and the reading pane for the currently-selected message goes blank.

**Replicate**
1. Open and read a message.
2. Wait for (or trigger) a background poll that refreshes the visible folder.
3. The body pane blanks even though the same message is still selected.

**Root cause**
- `RefreshCurrentFolderAsync` calls `Messages.Clear()` and rebuilds every VM from fresh IMAP envelopes. Envelopes carry headers/preview but **not** `BodyHtml`/`BodyPlain` (those are fetched separately by `SelectMessageAsync`). The reading pane then bound to a brand-new VM with an empty body.

**Tried**
- Nothing — diagnosed on first read. The Clear-and-rebuild in the refresh path dropping the separately-fetched body was the obvious cause; this is the auto-refresh-era sibling of FIX-2026-05-12-001.

**Fix**
- Snapshot `SelectedMessage`'s `BodyHtml` + `BodyPlain` before the clear; copy them onto the new VM during selection-restore; fire `OnBodyLoaded` so `MessageBodyView`'s `PropertyChanged` subscription (FIX-2026-05-12-001) re-renders. The displayed message stays visible across refreshes.

**Files changed**
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` — body snapshot + restore in `RefreshCurrentFolderAsync`.

**Rulebook**
- §2 Surgical Change Rule — refresh path only.
- §10 — when a collection rebuild discards async-fetched state, carry that state forward explicitly; don't assume the new VM has it.

**Session:** 2026-05-15
**Commit:** e3ca017

---

## FIX-2026-05-15-005 — "Auto Send/Receive" said "0 = manual only" but the poller silently clamped 0 to 1 minute

**Area:** Settings / SettingsViewModel + SettingsDialog · sync-interval honesty
**Status:** ✅ Fixed in v0.12.5
**Priority:** P2 (UI promised an off-switch that doesn't exist)

**Symptom**
- Settings → Send/Receive row read "0 = manual only", but entering 0 still polled every minute. `NewMailPoller` force-clamps via `Math.Max(1, SyncIntervalMinutes)`, so 0 was identical to 1.

**Replicate**
1. Set the Auto Send/Receive interval to 0 expecting manual-only.
2. Observe the app keeps polling once a minute.

**Root cause**
- The poller's `Math.Max(1, …)` clamp was correct defensive code, but the Settings description advertised a manual-only mode that was never implemented, and 0 was being persisted (and synced to Firestore) even though it could never take effect.

**Tried**
- Nothing — the mismatch between the description literal and the poller's clamp was a direct read. Owner chose "make the description honest" over "build a real manual-only mode" (the latter left as a separate future change: drop the clamp in `Reschedule` and gate `Start()` on non-zero).

**Fix**
- Description now reads "Check for new mail at this interval. Minimum 1 minute."
- `SettingsViewModel.OnSyncIntervalMinutesChanged` clamps to `>= 1` before writing to `AppSettings`, so the persisted (and synced) value matches what the poller actually runs.

**Files changed**
- `src/GreatEmailApp/ViewModels/SettingsViewModel.cs` — clamp on change.
- `src/GreatEmailApp/Views/Dialogs/SettingsDialog.xaml` — honest description text.

**Rulebook**
- §2 Surgical Change Rule — no behavior change to the poller; only the VM clamp + label.
- Sibling of the spirit behind FIX-2026-05-13-001: never let a visible literal advertise behavior the code doesn't deliver.

**Session:** 2026-05-15
**Commit:** e2d5579

---

## FIX-2026-05-15-006 — Search-box placeholder stayed visible after clicking in, hiding only on first keystroke

**Area:** Controls / TitleBar + MailList · search placeholder focus behavior
**Status:** ✅ Fixed in v0.12.5
**Priority:** P3 (polish; no focus cue against the dark background)

**Symptom**
- Click into the title-bar search box (or the mail-list one) and the placeholder text stays put, only disappearing on the first keystroke. With no clear cursor cue against the dark background, the user can't tell whether the box has focus.

**Replicate**
1. Click into a search box without typing.
2. Placeholder remains; no obvious focus indication.

**Root cause**
- Placeholder visibility was driven by imperative `TextChanged` handlers that only flipped on text content, never on focus.

**Tried**
- Nothing — straightforward: the placeholder needed to react to focus, not just text.

**Fix**
- Both placeholders are now driven by a `MultiDataTrigger` on the parent `TextBlock`'s `Style`: visible only when `Text == ""` **AND** the `TextBox` is not keyboard-focused. Clicking in immediately collapses the placeholder; clicking out with no text restores it. The prior imperative `TextChanged` handlers were removed.

**Files changed**
- `src/GreatEmailApp/Controls/TitleBar.xaml` + `TitleBar.xaml.cs` — `MultiDataTrigger` style; `SearchBox_TextChanged` no longer sets Visibility.
- `src/GreatEmailApp/Controls/MailList.xaml` + `MailList.xaml.cs` — same treatment; `ListSearchBox_TextChanged` handler removed.

**Rulebook**
- §2 Surgical Change Rule — declarative trigger replaces imperative handlers; no other behavior touched.
- §10 — prefer declarative XAML triggers for visual state that depends on focus/content.

**Session:** 2026-05-15
**Commit:** 99bfaab

---

## FIX-2026-05-13-001 — Title-bar "Sync on" chip lied about sync state before sign-in

**Area:** Controls / TitleBar · sync status chip
**Status:** ✅ Fixed
**Priority:** P2 (misleading UI; sync mechanics already worked)

**Symptom**
- Fresh install on a new PC, app launched from desktop shortcut: title-bar chip showed a green dot + "Sync on" before the user had signed into Firebase. Implied that settings/accounts/rules were syncing when nothing was — the user reasonably read it as "sync is not live yet" since sign-in hadn't happened.

**Replicate**
1. Fresh install (no `auth.dat`), launch app.
2. Look at the top-right title bar — green chip says "Sync on" with no qualifier.
3. There is no IMAP account or Firebase session yet.

**Root cause**
- [TitleBar.xaml:159](src/GreatEmailApp/Controls/TitleBar.xaml:159) had a hardcoded `<TextBlock Text="Sync on" />` and a green `<Ellipse Fill="{DynamicResource GreenBrush}" />` — pure visual decoration with no binding to auth or sync state.
- Spiritual sibling of FIX-2026-05-07-001 (avatar pretended a user was signed in when they weren't). That fix repaired the avatar code-behind but missed the visible chip literal in the title-bar XAML.

**Tried**
- Nothing — diagnosed on first read. Grep for `"Sync.{0,5}[Oo]n"` immediately surfaced the hardcoded literal in TitleBar.xaml; cross-checked with SettingsViewModel/SyncCoordinator confirmed sync wiring itself was already correct.

**Fix**
- `MainViewModel`: added `SyncIndicatorVisible` / `SyncIndicatorText` / `SyncIndicatorBrush` / `SyncIndicatorTooltip` observable properties, plus `UpdateSyncIndicator(SyncEvent?)` driven by `App.Auth.SessionChanged` AND `App.SyncCoordinator.StateChanged`. Computes state from `IsSignedIn` + latest `SyncEventKind`.
- `TitleBar`: added 4 matching `SyncChip*` DPs following the existing `AccountInitial`/`AccountEmail` pattern. XAML chip's `Visibility`, `Ellipse.Fill`, `TextBlock.Text`, and `ToolTip` now bind to these DPs. Local `BoolVis` converter declared in `UserControl.Resources`.
- `MainWindow.xaml`: wired MainViewModel's `SyncIndicator*` properties into TitleBar's `SyncChip*` DPs.
- Sync mechanics (SyncCoordinator, FirestoreSyncService, push/pull paths, vault) — untouched. Pure visual binding fix.

**Behavior now:**
| Auth | Coordinator | Chip |
|------|-------------|------|
| Signed-out | — | hidden |
| Signed-in | Idle / Pushed / Applied | green · "Sync on" · "Firebase sync is live. Settings, accounts, contacts, and rules sync across PCs." |
| Signed-in | Pushing / Pulling | accent · "Syncing…" |
| Signed-in | Failed | red · "Sync error" + detail tooltip |

**Files changed**
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` (Rev 6 → 7)
- `src/GreatEmailApp/Controls/TitleBar.xaml.cs` (Rev 4 → 5)
- `src/GreatEmailApp/Controls/TitleBar.xaml` (Rev 1 → 2)
- `src/GreatEmailApp/MainWindow.xaml` (Rev 2 → 3)

**Rulebook**
- §2 Surgical Change Rule — only the 4 binding-related files touched; sync pipeline left alone.
- §10 (Components & UI/UX) — reinforces: never let visible literals advertise state that isn't bound to a real source of truth. Same anti-pattern as FIX-2026-05-07-001 (avatar).

**Session:** 2026-05-13
**Commit:** _pending_

---

## FIX-2026-05-13-002 — "Look for an app in the Microsoft Store" popup after sign-in (mailto:/non-http schemes from email bodies)

**Area:** Controls / MessageBodyView + RichTextEditor · WebView2 link handoff
**Status:** ✅ Fixed
**Priority:** P1 (visible on every email with a mailto: or non-http URI)

**Symptom**
- Windows dialog: "Your PC doesn't have an app that can open this link. Try looking for a compatible app in the Microsoft Store."
- Pops up autonomously after sign-in on a fresh install. Also observed on a second PC running the app yesterday.
- No new-mail balloon visible at the same time. Disabling new-mail notifications in Settings did NOT stop the popup.

**Replicate**
1. Sign in with an account that has HTML emails containing `mailto:` / `tel:` / `webcal:` / tracking-scheme links (any typical marketing email).
2. Select a message in the reading pane, OR let one with `<meta http-equiv="refresh">` auto-fire `OnNavigationStarting`.
3. Popup appears.

**Root cause**
- [MessageBodyView.xaml.cs:225 + :237](src/GreatEmailApp/Controls/MessageBodyView.xaml.cs:225) called `Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true })` for **any** URI WebView2 hands them — no scheme validation. Email-controlled URIs with no registered Windows handler (mailto: without a default mail app, tel:, webcal:, outlook:, tracking schemes) fall back to the shell's "Look for an app in the Microsoft Store" dialog.
- [RichTextEditor.xaml.cs:78](src/GreatEmailApp/Controls/RichTextEditor.xaml.cs:78) had the same hole in the compose-window editor.
- Same bonus security issue: blindly shelling out email-controlled URIs lets a malicious sender invoke any registered scheme handler (`javascript:`, `file:`, custom schemes) via `UseShellExecute=true`.

**Tried**
- **Hypothesis A — modern toast activator (per FIX-2026-05-12-002's wording).** Added `Services/ToastAumid.cs`: `SetCurrentProcessExplicitAppUserModelID("Fiksdit.TheGreatEmailApp")` + writes a Start-menu .lnk with `PKEY_AppUserModel_ID`. Wired from `App.OnStartup`. Build clean, shortcut created at `%APPDATA%\Microsoft\Windows\Start Menu\Programs\The Great Email App.lnk`. Popup still appeared. **Hypothesis rejected** — modern toasts on H.NotifyIcon weren't the trigger.
- **Hypothesis B — Windows toast pipeline regardless of visibility.** Asked user to toggle off new-mail notifications and reproduce. Popup still appeared with notifications fully off. Toast pipeline definitively ruled out.
- Then grep for `Process.Start.*UseShellExecute` surfaced the MessageBodyView call sites; HTML email auto-refresh / mailto: handoff was the obvious match for "autonomous + no visible balloon."

**Fix**
- `MessageBodyView.xaml.cs`: extracted `OpenExternal(string? uri)` helper that whitelists `http`/`https` only. Drops other schemes silently. Both `OnNavigationStarting` and `OnNewWindowRequested` route through it.
- `RichTextEditor.xaml.cs`: inline scheme whitelist in the `NewWindowRequested` handler.
- `Services/ToastAumid.cs` and the `App.OnStartup` call are **kept** as defense-in-depth — they're correct shell hygiene for future toast click-to-open work, even though they weren't the popup's actual fix.

**Behavior change (acceptable, per owner):** `mailto:` links inside email bodies now silently no-op instead of trying to launch a system mail client. Follow-up: route mailto: into an in-app Compose window (separate task).

**Files changed**
- `src/GreatEmailApp/Controls/MessageBodyView.xaml.cs` (Rev 2 → 3) — scheme whitelist
- `src/GreatEmailApp/Controls/RichTextEditor.xaml.cs` (Rev 1 → 2) — scheme whitelist
- `src/GreatEmailApp/Services/ToastAumid.cs` (new) — AUMID + Start-menu .lnk (defense-in-depth)
- `src/GreatEmailApp/App.xaml.cs` (Rev 10 → 11) — calls `ToastAumid.EnsureRegistered()` early in OnStartup

**Rulebook**
- §2 Surgical Change Rule — diagnosis ruled out toast pipeline before code changes landed in MessageBodyView; AUMID work that did land is kept as legitimate hygiene rather than reverted.
- §5 Routes & Security — `UseShellExecute=true` on attacker-controlled URIs is now scoped to http(s) only.
- §11 Error Handling — non-http schemes drop silently; never crash on a link click.
- Spiritual update to FIX-2026-05-12-002's diagnosis: that entry blamed the toast-activation URI handler under crash conditions. With the crash gone, autonomous popups had a different (and previously unknown) cause: the MessageBodyView shell-out path.

**Session:** 2026-05-13
**Commit:** _pending_

---

## FIX-2026-05-12-002 — Tray notifier still crashed on cold start; added ForceCreate on top of FIX-2026-05-10-002

**Area:** Services / TrayNotifier · NewMailPoller balloon delivery
**Status:** ✅ Fixed
**Priority:** P1 (crashes the process on idle)

**Context**
- FIX-2026-05-10-002 wrapped `ShowNotification` in try/catch so the throw stopped killing the process. But the native tray slot was *still* never being registered — H.NotifyIcon's WPF `TaskbarIcon` defers `Shell_NotifyIcon` creation to its Loaded event, and we instantiate the icon from code with no XAML host. So toasts were being silently swallowed AND no tray icon ever appeared in the system tray.

**Symptom (still observed today, even with FIX-2026-05-10-002 deployed locally)**
- `%LOCALAPPDATA%\GreatEmailApp\crash.log` between 10:53 and 11:04 on 2026-05-12 had three identical stacks:
  ```
  System.InvalidOperationException: TrayIcon is not created.
    at H.NotifyIcon.Core.TrayIcon.EnsureCreated()
    at H.NotifyIcon.TaskbarIcon.ShowNotification(...)
    at GreatEmailApp.Services.TrayNotifier.ShowBalloon(...)
  ```
- Windows then surfaced "Look for an app in the Microsoft Store to open this link" — the .NET crash recovery path falling through to the toast-activation URI handler that has no registered protocol for this raw (non-MSIX, no AppUserModelID) EXE.

**Tried**
- Nothing — diagnosed straight from the crash.log stacks plus the H.NotifyIcon source pointer in the trace.

**Fix**
- `TrayNotifier` ctor now calls `_icon.ForceCreate()` immediately after configuring icon + menu, wrapped in try/catch with a `crash.log` append. This proactively registers the native tray slot at startup instead of waiting on a never-firing Loaded event, so subsequent `ShowNotification` calls find a created icon. The catch path keeps the constructor unkillable.
- `ShowBalloon` already had the `try / catch (InvalidOperationException) / catch (Exception)` from FIX-2026-05-10-002 — kept in place as belt-and-suspenders.

**Files changed**
- `src/GreatEmailApp/Services/TrayNotifier.cs` (Rev 3 → 4)

**Rulebook**
- §11 (Error Handling) — toast/balloon delivery is non-critical I/O; never propagate to a process-killing exception.
- §2 Surgical Change Rule — only TrayNotifier touched.

**Session:** 2026-05-12

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
- `SelectMessageAsync` sets `SelectedMessage = B` first → WPF re-binds `ReadingPane`'s inner DataContext → `MessageBodyView.Message` DP changes → `OnMessageChanged` fires → `Render()` runs **synchronously** reading `B.BodyHtml`/`B.BodyPlain`, both still empty at this point → WebView2 renders the empty wrapper doc.
- THEN `_imap.FetchBodyAsync(B, …)` returns ~200 ms later → assigns the strings on the model → calls `B.OnBodyLoaded()` which fires `PropertyChanged` for `BodyHtml`, `BodyPlain`, `BodyDisplay`.
- **Nothing in `MessageBodyView` listened for those events.** Its only render trigger was the `Message` DP changed callback — and the DP value (the `MessageViewModel` reference) was unchanged.
- `MainViewModel` tried to nudge things with `OnPropertyChanged(nameof(SelectedMessage))`, but the binding's new value reference-equals the old, so WPF's DP system short-circuits and `OnMessageChanged` isn't re-invoked.

**Tried**
- Nothing — diagnosed on first read of `MessageBodyView.Render()` + `OnMessageChanged` + the body-fetch tail of `SelectMessageAsync`. The control's `Refresh()` method existed but was never called, which was the tell.

**Fix**
- `MessageBodyView` now tracks `_subscribedMessage` and subscribes to `INotifyPropertyChanged.PropertyChanged` on the bound `MessageViewModel`. When `BodyHtml`/`BodyPlain`/`BodyDisplay` fires, it re-calls `Render()`. Switching messages detaches the previous subscription first to prevent leaks.

**Files changed**
- `src/GreatEmailApp/Controls/MessageBodyView.xaml.cs` (Rev 1 → 2)

**Rulebook**
- §2 Surgical Change Rule — touched only the file with the missing wiring.
- §10 (Components & UI/UX) → reinforces: DP-only re-render is a trap when the model behind the DP mutates async. Listen to `PropertyChanged` on the model when its inner state can change after the DP is set.

**Session:** 2026-05-12

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
