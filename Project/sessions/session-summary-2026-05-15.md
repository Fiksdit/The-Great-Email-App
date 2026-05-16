<!-- FILE: Project/sessions/session-summary-2026-05-15.md
     Created: 2026-05-15 | Revised: 2026-05-15 | Rev: 1
     Changed by: Claude Opus 4.7 on behalf of James Reed -->

# Session Summary — 2026-05-15
**Project:** The Great Email App
**Duration:** ~7 hours (09:00 → 15:45 local)
**Engineer:** James Reed + Claude Opus 4.7
**Outcome:** v0.12.5 shipped to GitHub; spam filter Phase 1 + cross-folder server search + 6 supporting fixes live in production.

---

## What Was Done

| # | Item | Files Changed | Status |
|---|------|--------------|--------|
| 1 | Bundle 5 fixes carried over from prior session (cached-envelope paint, WebView2 nav serialization, tooltip skin, sync tooltip legend, mail-list selection border, primary-account inbox auto-select) | `IMessageCache.cs`, `SqliteMessageCache.cs`, `MailList.xaml`, `MessageBodyView.xaml.cs`, `TitleBar.xaml`, `Controls.xaml`, `MainViewModel.cs` | ✅ Done (`035f8d2`) |
| 2 | Phase 1 spam filter — `Core/Spam/` (9 new files), classifier service, ~30 default keywords, auto-route Spam verdicts to Junk with \Seen flag, trusted-sender bootstrap from Sent folder | new: `SpamVerdict.cs`, `SpamFilterConfig.cs`, `ISpamFilter.cs`/`SpamFilter.cs`, `ISpamFilterEngine.cs`/`SpamFilterEngine.cs`, `ISpamConfigStore.cs`/`JsonSpamConfigStore.cs`; modified: `AppPaths.cs` (R5), `AppSettings.cs` (R3), `App.xaml.cs` (R12), `FolderViewModel.cs` (R4) | ✅ Done (`69a0ac9`) |
| 3 | Auto-refresh mail list on `MessagesPolled` + first batch of investor-cold-outreach keywords | `MainViewModel.cs` (R9), `SpamFilterConfig.cs`, `JsonSpamConfigStore.cs` | ✅ Done (`6250f94`) |
| 4 | Body-preservation snapshot in `RefreshCurrentFolderAsync` + 20 new keywords (business-acquisition, manufacturer outreach, phishing CTA) | `MainViewModel.cs` (R10), `SpamFilterConfig.cs` | ✅ Done (`e3ca017`) |
| 5 | ISO 8601 chronological cache sort (`Message.SentAt` field, ImapService sets, SqliteMessageCache writes/reads ISO, self-healing WHERE-LIKE filter) + FolderLoaded event → ScrollViewer.ScrollToTop() on folder click | `Message.cs` (R2), `ImapService.cs` (R3), `SqliteMessageCache.cs` (R3), `MainViewModel.cs` (R11), `MailList.xaml` (R3), `MailList.xaml.cs` (R6) | ✅ Done (`0e81369`) |
| 6 | Settings Auto Send/Receive — description fix + VM-side clamp ≥1 | `SettingsDialog.xaml`, `SettingsViewModel.cs` | ✅ Done (`e2d5579`) |
| 7 | **Decouple poll cycle from notification toggle** (biggest find of the session) | `NewMailPoller.cs` (R2), `TrayNotifier.cs` (R5) | ✅ Done (`70e024f`) |
| 8 | Cross-folder IMAP server search (`IImapService.SearchAccountAsync` + `MainViewModel.SearchServerCommand` + bottom-docked Hyperlink in `MailList`) | `IImapService.cs` (R3), `ImapService.cs`, `MainViewModel.cs` (R12), `MailList.xaml` (R4) | ✅ Done (`d536bfa`) |
| 9 | Keywords round 3: mailbox-quota phishing patterns (12 new) | `SpamFilterConfig.cs` (R4) | ✅ Done (`6d4e648`) |
| 10 | Search placeholder hides on focus (MultiDataTrigger in both TitleBar + MailList) | `TitleBar.xaml` (R4), `TitleBar.xaml.cs`, `MailList.xaml` (R5), `MailList.xaml.cs` (R7) | ✅ Done (`99bfaab`) |
| 11 | v0.12.5 release (csproj bump, roadmap What's New, publish script glyph fix for PS 5.1) | `GreatEmailApp.csproj`, `Project/roadmap.md`, `scripts/publish.ps1` | ✅ Done (`d6e10f9`) |
| 12 | Sprint plan for week of 2026-05-18 generated (12 tasks Mon→Fri) | new: `Project/team/james/sprint-2026-05-18.md` | ✅ Done (`de5c090`) |

---

## Commits Pushed

| SHA | Message |
|-----|---------|
| `035f8d2` | Bundle five small fixes from prior session |
| `69a0ac9` | Add Phase 1 spam filter |
| `6250f94` | Auto-refresh mail list on poll + spam keywords for investor cold-outreach |
| `e3ca017` | Preserve body during auto-refresh + spam keywords round 2 |
| `0e81369` | Fix mail-list scroll-to-top + chronological cache sort |
| `e2d5579` | Fix Auto Send/Receive description + VM clamp |
| `70e024f` | Decouple poll cycle from notification toggle |
| `d536bfa` | Add 'Search server for more results' (cross-folder IMAP search) |
| `6d4e648` | Spam keywords round 3: mailbox-quota phishing |
| `99bfaab` | Search placeholder now hides on focus, not just on type |
| `d6e10f9` | Release v0.12.5 |
| `de5c090` | docs(sprint): generate week of 2026-05-18 sprint file (12 tasks, solo) |

12 commits. Release `v0.12.5` published at <https://github.com/Fiksdit/The-Great-Email-App/releases/tag/v0.12.5>.

---

## Bugs Fixed

> **Note:** Structured `FIX-YYYY-MM-DD-NNN` entries in `Project/logs/fix_log.md` are **not yet written** for the 8 bug-class changes below. Carry-forward into next sprint as **T1** (already on the sprint file).

| Internal ID | Description | Root Cause |
|--------|-------------|-----------|
| (pending) FIX-2026-05-15-001 | Notification toggle silently disabled the whole polling subsystem | `NewMailPoller.PollOnceAsync` had `if (!EnableNewMailNotifications) return;` — gating cache writes, spam filter, rules engine, auto-refresh. Decoupled by moving the gate to `TrayNotifier.OnNewMail`. |
| (pending) FIX-2026-05-15-002 | Cache surfaced "May 6" emails when reopening Inbox | `SqliteMessageCache` stored `FullTime` display string in `sent_at`; `ORDER BY sent_at DESC` sorted alphabetically by day-of-week ("Wed" > "Tue" > "Thu" > ...). Replaced with ISO 8601 + WHERE-LIKE filter to skip legacy rows; cache self-heals on next poll. |
| (pending) FIX-2026-05-15-003 | Mail list didn't always scroll to top on folder click | `<ScrollViewer>` wrapping the ItemsControl had no signal to reset on `Messages.Clear()` + re-Add. Added `MainViewModel.FolderLoaded` event raised from `SelectFolderAsync` (not `RefreshCurrentFolderAsync`); `MailList.xaml.cs` subscribes and calls `MessageScroll.ScrollToTop()`. |
| (pending) FIX-2026-05-15-004 | Reading pane went blank during background auto-refresh | `RefreshCurrentFolderAsync` rebuilt `MessageViewModel`s from fresh IMAP envelopes that lacked `BodyHtml`/`BodyPlain`. Snapshot the selected message's body before `Clear()`, copy it onto the rebuilt VM, fire `OnBodyLoaded()`. |
| (pending) FIX-2026-05-15-005 | Settings "0 = manual only" description lied about behavior | `NewMailPoller.Reschedule` clamped via `Math.Max(1, ...)` so 0 was silently 1. Description corrected; VM-side clamp added so persisted/synced value matches what runs. |
| (pending) FIX-2026-05-15-006 | Search placeholder lingered until first keystroke | Placeholder visibility driven only by Text emptiness. Replaced with XAML `MultiDataTrigger` checking both `Text == ""` AND `IsKeyboardFocusWithin == False`. Applied in both TitleBar and MailList search. |
| (pending) FIX-2026-05-15-007 | `scripts/publish.ps1` failed on Windows PowerShell 5.1 | UTF-8 glyphs (`✓`, `→`) parsed as mojibake in PS 5.1, breaking the script downstream. Replaced with plain ASCII. |
| (pending) FIX-2026-05-15-008 | Multi-account setups landed in random inbox at startup | Whichever account's IMAP `LIST` answered first won `SelectedFolder`. Added `autoSelectInbox` flag to `LoadFoldersAsync`, only the first stored account gets `true`. |

---

## Bugs Discovered (Open)

| # | Description | Priority | Next Step |
|---|-------------|---------|-----------|
| _none_ — all issues raised this session were fixed during the session | — | — |

---

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Spam filter built as separate keyword + heuristic classifier (P1-16), not bolted onto rules engine or AI classifier | Owner explicit: "rules are for rules and the learning is for giving me priority messages. but spam is for filtering out the keywords." Three separate concerns, three separate paths. |
| Default spam action = move to Junk + mark read, no auto-delete | Conservative; user already has Junk folder from Outlook; never silently destroys mail on first ship. |
| Trusted-sender bootstrap from Sent folder on first run | "Anyone you've ever emailed is auto-trusted." Avoids false positives on real correspondents without manual list curation. |
| App-open implies polling on; no manual-only mode | Owner: "i can't see a scenario where i would want the app open and not sending and receiving messages." Saved as a feedback memory. |
| Settings → Auto Send/Receive description fix over building a real off-switch | Owner picked honest-description option. Memory feedback captured separately. |
| Patch bump v0.12.5 (not minor v0.13.0) for the session's work | Owner preference — stay incremental on the 0.12 line. |
| Sprint week 2026-05-18 focuses on closing the spam-filter loop (Phase 2-4) + keyboard shortcuts; defers local FTS to next sprint | Avoid over-stuffing; one sprint, one coherent theme. |

---

## Rulebook Changes

- **Bumped to Rev 2** (was Rev 1).
- **§19 Lessons Learned** — added four entries:
  1. Pipeline coupling — never gate background subsystems on UI delivery toggles
  2. SQLite sort columns must be written in sortable representation
  3. ObservableCollection rebuild loses lazy-loaded state on destroyed VMs
  4. PowerShell scripts shouldn't use non-ASCII glyphs unless PS 7+ is guaranteed
- **§8 Known Issues** — no change (still empty; today's bugs were diagnosed AND fixed in-session).

---

## Memory Changes

- Saved feedback memory `feedback_app_open_implies_polling.md` — "App open implies auto Send/Receive is on; tunable axis is interval, not on/off."
- Updated `MEMORY.md` index.

---

## Roadmap Changes

- **P1-5c** Cross-folder + cross-account search — `📋 PLANNED` → `⚠️ PARTIAL` (cross-folder same-account shipped in v0.12.5; cross-account + local-FTS-first remain).
- **P1-16 (new)** Built-in spam filter — added at `⚠️ PARTIAL` (Phase 1 shipped, Phases 2-5 scheduled).
- **Shipped Log** — five rows added covering Phase 1 spam filter, partial cross-folder search, auto-refresh, poller decoupling, ISO cache sort.
- **What's New** — v0.12.5 entry added with full customer-facing changelog (also mirrored to the GitHub Release notes).
- Roadmap header date bumped 2026-05-10 → 2026-05-15.

---

## Next Session Must Do First

1. **T1 from sprint file**: catch-up `fix_log.md` with the 8 `FIX-2026-05-15-NNN` entries above before any new work begins. Rulebook §16 mandates structured entries per bug; we owe 8.
2. **T2 from sprint file**: Settings → Spam tab XAML skeleton (toggle, threshold, Junk-badge toggle) + `SettingsViewModel.SpamConfig` plumbing.
3. Run `/preflight` at session start (confirm clean git, verify which build user is launching, etc.).

The full week is laid out in [Project/team/james/sprint-2026-05-18.md](../team/james/sprint-2026-05-18.md) — 12 tasks across Mon–Fri ending with v0.13.0 cut.

---

## Blockers

| Blocker | Waiting On |
|---------|-----------|
| _none_ | — |

---

## Telemetry / Health Notes

- **`EnableNewMailNotifications` was OFF on the user's machine** at session start, which silently disabled the entire polling pipeline (cache, indexer, rules, auto-refresh, spam filter). User likely turned it off at some point thinking it was an unrelated UI preference. After today's decoupling fix, polling runs regardless. User still has the toggle off as of EOD — toast balloons remain suppressed by their preference. If the user wants balloons, they flip it on in Settings → Notifications.
- **Cache rows from before today** still carry display-string `sent_at` values. They're filtered out of cache-paint queries by the new `WHERE sent_at LIKE '____-__-__T%'` clause; ON-CONFLICT-DO-UPDATE on each poll rewrites them to ISO. Full self-healing expected within ~1 poll cycle per row touched, or weeks for cold messages — but cold messages don't surface in the top-25 cache-paint anyway.
- **Spam filter has been actively running** since `70e024f` (poll-decoupling fix). Owner should glance at the Junk folder over the weekend to see what's been caught and flag any false positives.

---

## Tally

- 12 commits, 1 release (`v0.12.5`), 1 sprint plan
- 9 new files + ~15 modified files
- 4 lessons-learned entries added to rulebook
- 2 feedback memories saved
- 8 bug-class changes shipped (fix log entries owed → T1 next sprint)
- 2 new product capabilities: built-in spam filter + cross-folder server search
