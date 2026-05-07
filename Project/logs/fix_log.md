# Fix Log

Permanent record of bug-class changes per rulebook §16. Newest first.

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
