# Fix Log

Permanent record of bug-class changes per rulebook §16. Newest first.

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
