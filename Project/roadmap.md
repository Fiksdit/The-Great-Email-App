# The Great Email App — Master Roadmap
**Created:** 2026-04-29 | **Updated:** 2026-05-10
**Stack:** WPF + .NET 8 (C#) + MailKit + SQLite + Firebase
**Owner:** James Reed (coolman0804@outlook.com)
**Vision:** A clean, fast, native-Windows IMAP email client with Outlook's familiar ribbon UX, dark/light theming, and Firebase-backed settings sync across multiple PCs.

---

## Vision Statement

The Great Email App (TGEA) is a focused desktop email client for power users who run multiple IMAP accounts and want Outlook's familiar ribbon-and-folder layout without the bloat of calendar/tasks/teams. Settings sync via Firebase so installing on a new PC restores all account configs (passwords stay local, in Windows Credential Manager). Built for fiksdit.com IMAP first, but works with any IMAP server.

### What this app is *really* about

**Search and notifications.** Every other feature serves these two:

1. **The world's best email search.** Years of mail, scanned in milliseconds, with **zero false positives**. No remembering query syntax. No typing `from:` `subject:` `before:`. Just type what you mean and pick what you're searching across — sender, subject, body, date — and the program does the rest. Partial matches by default; an "exact" toggle when you need it. The underlying index lives locally; disk size is not a constraint. Old data can be cleaned up on demand, but the default is to keep everything searchable forever.

2. **Important mail surfaced fast.** Real-time notifications for senders and subjects that matter, learned from the user's actual reading behavior — not from heuristics that flag every newsletter. Ads, marketing, and routine junk are demoted; people you reply to and threads you read are promoted. The user shouldn't have to configure rules to make this work.

These two principles set the bar for every roadmap item: if a change makes search slower, less accurate, or makes notifications noisier, it does not ship.

---

## Priority Tiers

- 🔴 **P0 — Launch Blockers** (must ship before first real user)
- 🟠 **P1 — High Impact** (core email workflow)
- 🟡 **P2 — Competitive Parity** (matches Outlook features)
- 🟢 **P3 — Differentiators** (where TGEA pulls ahead)

---

## 🔴 P0 — Launch Blockers

| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P0-1 | WPF shell — title bar, ribbon, three-pane layout, status bar | 🔧 IN PROGRESS | Phase 1 |
| P0-2 | Light/dark theme + accent color, tokens from design | 🔧 IN PROGRESS | Phase 1 |
| P0-3 | Resizable panes (sidebar 200–380, mail list 300–560) | 🔧 IN PROGRESS | Phase 1 |
| P0-4 | Add Account dialog with IMAP/SMTP fields + Test Connection | 📋 PLANNED | Phase 2 |
| P0-5 | Windows Credential Manager integration for passwords | 📋 PLANNED | Phase 2 |
| P0-6 | IMAP connection (MailKit) — list folders, fetch messages | 📋 PLANNED | Phase 2 |
| P0-7 | Local SQLite cache for messages | 📋 PLANNED | Phase 2 |
| P0-8 | Send mail via SMTP | 📋 PLANNED | Phase 2 |
| P0-9 | Settings dialog (General, Accounts, Appearance, Sync) | 📋 PLANNED | Phase 3 |
| P0-10 | Settings persistence (`settings.json` in `%LOCALAPPDATA%`) | 📋 PLANNED | Phase 3 |
| P0-11 | Google sign-in (Firebase Auth) | 📋 PLANNED | Phase 4 |
| P0-12 | Firestore settings sync (push/pull on change + on launch) | 📋 PLANNED | Phase 4 |
| P0-13 | First-run sign-in screen with skip path | 📋 PLANNED | Phase 4 |
| P0-14 | **Encrypted IMAP-password sync via passphrase-gated Firestore vault** | ✅ DONE | 2026-05-07. Argon2id + AES-256-GCM. Settings → Sync → "Set up password sync" / "Unlock with passphrase" / "Resync passwords". Replaces the original "passwords never leave the device" rule — see rulebook §7C and decision log. |

**Status key:** `📋 PLANNED` · `🔧 IN PROGRESS` · `⚠️ PARTIAL` · `✅ DONE`

---

## 🟠 P1 — High Impact

Core email workflow that makes the app actually usable.

| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P1-1 | Compose window (new email, reply, reply all, forward) | 📋 PLANNED | |
| P1-2 | Attachment handling (download, preview, attach to outgoing) | 📋 PLANNED | |
| P1-3 | Mark read/unread, flag, archive, delete | 📋 PLANNED | |
| P1-4 | Move-to-folder | ⚠️ PARTIAL | Right-click → Move to (nested submenus, v0.11.7) works. **Toolbar Move button is not wired up** — clicking it does nothing. Needs: (a) hook ribbon Move button to a flyout/menu reusing the same folder-tree picker, (b) honor multi-select once P1-12 lands. |
| P1-4b | Drag-and-drop email → folder | 📋 PLANNED | Drag from MailList row(s) onto a Sidebar folder node to move (Shift+drag = copy, where IMAP supports it). Needs: DragDrop on MailList row, drop-target highlighting on FolderRow, multi-select drag (depends on P1-12), cross-account drag explicitly blocked (IMAP can't atomically move across accounts). |
| P1-5 | Basic in-folder search (sender / email / subject only — strict) | ⚠️ PARTIAL | v0.12.0 wires up the in-list search box, scoped to current folder, matching sender display name + sender email + subject. Body is **deliberately excluded** to avoid false positives. Case-insensitive contains. See Search Roadmap below for the rest of the journey. |
| P1-5a | **Advanced search builder** (toolbar dialog) | 📋 PLANNED | A dialog/flyout with a row per criterion the user picks: *date range*, *sender name*, *sender email*, *subject*, *body*, *folder scope*, *has attachment*, *flagged*, *read state*. Each row defaults to **partial match**, with an **"Exact"** toggle. Multiple rows AND together; an "Any of these" group switches to OR. No query syntax to memorize. Replaces the legacy `from:foo subject:bar` formula. |
| P1-5b | Local full-text index (Lucene.NET or SQLite FTS5) | 📋 PLANNED | Index every message body the user is likely to search. Built incrementally on poll; rebuildable from cache. Disk usage **not a constraint** — accuracy and speed first. Indexing happens off the UI thread; results stream back. See "Search & Index Strategy" below. |
| P1-5c | Cross-folder + cross-account search | 📋 PLANNED | "Search all mail" / "Search this account" pickers. Default scope = current folder; explicit broaden when the user asks. |
| P1-5d | Junk/marketing demotion in default search scope | 📋 PLANNED | Search excludes Junk + folders the user never opens (read-frequency learning, P1-5e) by default. Toggle "Include all" reveals them. Cuts the "8 million false positives" problem at the source. |
| P1-5e | **Read-frequency learning** | 📋 PLANNED | Per-folder, per-sender open/read counters update on every Open and OnReadStateChanged. The signal feeds: (a) search-scope demotion (P1-5d), (b) notification prioritization (P1-7b), (c) a future "Important" view. Stored locally in SQLite — never synced (per-PC behavior). |
| P1-6 | Auto sync interval (configurable polling per account) | 📋 PLANNED | |
| P1-7 | IMAP IDLE for real-time push where supported | 📋 PLANNED | |
| P1-7b | **Smart notification prioritization** | 📋 PLANNED | Notifications fire instantly for senders/threads with a positive read-frequency signal (P1-5e). Ads/marketing/auto-replies are silently delivered without a balloon. User-tunable "always notify" + "never notify" lists in Settings → Notifications. |
| P1-6 | Auto sync interval (configurable polling per account) | 📋 PLANNED | |
| P1-7 | IMAP IDLE for real-time push where supported | 📋 PLANNED | |
| P1-8 | New mail notifications (Windows toast) | 📋 PLANNED | |
| P1-9 | HTML email rendering with remote-image gating | 📋 PLANNED | WebView2 surface; off when Settings.ShowHtml=false |
| P1-10 | Backstage view (File tab) | 📋 PLANNED | |
| P1-11 | Keyboard shortcuts (Ctrl+R reply, Ctrl+Enter send, Del delete, F5 send/receive, Ctrl+Shift+M new) | 📋 PLANNED | |
| P1-12 | Multi-select in mail list (Ctrl+click, Shift+click) + batch archive/delete/move | 📋 PLANNED | |
| P1-13 | First-run onboarding when launched with zero accounts | 📋 PLANNED | Replaces sample data with a guided Add Account flow |
| P1-14 | App icon + branded taskbar/installer presence | 📋 PLANNED | .ico + AppxManifest fields |
| P1-15 | Brand the Google OAuth consent screen | 📋 PLANNED | Currently shows the GCP project ID (`project-6464…`) during sign-in. Set **App name = "The Great Email App"**, support email, logo, privacy/TOS URLs in GCP → APIs & Services → OAuth consent screen. If publishing status is "In production," any change re-triggers Google verification (days). Easier while still in "Testing." No code/rebuild needed. |

---

## 🟡 P2 — Competitive Parity

| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P2-1 | Rules / filters | 📋 PLANNED | |
| P2-2 | Signatures (per account, per reply-vs-new) | 📋 PLANNED | |
| P2-3 | Conversation view (threading) | 📋 PLANNED | |
| P2-4 | Drafts auto-save | 📋 PLANNED | |
| P2-5 | Address book / contacts | 📋 PLANNED | |
| P2-6 | Print preview + print | 📋 PLANNED | |
| P2-7 | Import from .pst / .mbox | 📋 PLANNED | |
| P2-8 | Export to .eml | 📋 PLANNED | |
| P2-9 | Multiple identities per account (alias send-as) | 📋 PLANNED | |
| P2-10 | Density options (Compact / Cozy / Comfortable) | 📋 PLANNED | Setting exists; needs row-template adjustment |
| P2-11 | Drag-and-drop — message → folder, file → compose | 📋 PLANNED | |
| P2-12 | Conversation/threading view | 📋 PLANNED | Group reply chains by Message-ID/References |
| P2-13 | Folder operations (real) — New/Rename/Delete/Empty via MailKit | 📋 PLANNED | UI stubs already present in folder context menu |
| P2-14 | Backup & restore — export/import accounts.json + settings.json | 📋 PLANNED | Useful pre-Firebase-sync, also covers users who skip Firebase |
| P2-15 | Account import from existing Outlook profile | 📋 PLANNED | Read HKCU registry under Office/Outlook/Profiles, pre-fill IMAP/SMTP. Big "wow" for migrators. |
| P2-16 | **Aggressive local message cache** | 📋 PLANNED | Pull as much from IMAP as the server allows (envelopes always; bodies on first open or background) and store locally in SQLite for **offline + searchable forever**. Disk usage is not a constraint by design — see vision statement. Cache is keyed `(accountId, folder, uid)`; survives folder rename via UIDVALIDITY tracking. |
| P2-17 | **Data cleanup / retention controls** | 📋 PLANNED | Settings → Storage panel: per-account "Keep messages from the last [N] years / months / forever," "Clear cached bodies older than X" (envelopes/index entries kept), "Drop folder X from cache." Index re-builds on demand. Default = keep everything. |

### Technical Debt
| ID | Item | Status | Notes |
|----|------|--------|-------|
| P2-TD-1 | Unit tests for Core (services, parsers) | 📋 PLANNED | xUnit. Bare minimum: round-trip JsonAccountStore / JsonSettingsStore / WindowsCredentialStore. |
| P2-TD-2 | UI tests (Appium / FlaUI) for critical flows | 📋 PLANNED | Add Account, send, receive, archive |
| P2-TD-3 | Crash reporting (Sentry or similar) | 📋 PLANNED | |
| P2-TD-4 | Auto-update mechanism (Velopack preferred over Squirrel) | 📋 PLANNED | |
| P2-TD-5 | Replace `Console.Error.WriteLine` with `Microsoft.Extensions.Logging` per rulebook §11 | 📋 PLANNED | |
| P2-TD-6 | CI build check — `.github/workflows/build.yml` runs `dotnet build` on push | 📋 PLANNED | Catches broken builds before merge |
| P2-TD-7 | Code signing for the installer | 📋 PLANNED | Requires cert |
| P2-TD-8 | Tests project skeleton at `tests/GreatEmailApp.Tests/` | 📋 PLANNED | Referenced in rulebook §3 but doesn't exist yet |
| P2-TD-9 | MailKit transitive BouncyCastle advisory (GHSA-9j88-vvj5-vhgr) | 📋 PLANNED | Currently a build warning. Track upstream fix in MailKit 4.14+. |

---

## 🟢 P3 — Differentiators

| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P3-1 | Per-sender remote-image trust list (synced) | 📋 PLANNED | |
| P3-2 | Quick filters (unread/flagged/has-attachment) as global hotkeys | 📋 PLANNED | |
| P3-3 | Markdown compose mode | 📋 PLANNED | |
| P3-4 | OAuth2 IMAP (Google, Microsoft) for accounts that require it | 📋 PLANNED | |
| P3-5 | End-to-end encrypted notes attached to messages (local-only) | 📋 PLANNED | |
| P3-6 | Plugin/extension API | 📋 PLANNED | |

### AI / Automation

| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P3-AI-1 | **Rules engine** — user-defined IF/THEN rules (sender / subject / body / has-attachment → move / flag / mark read / forward / run-rule) | 📋 PLANNED | Server-side IMAP filters where possible, client-side fallback. UI: Rules dialog with rule list + builder. |
| P3-AI-2 | **Ollama integration** — local LLM helps prioritize and sort the inbox throughout the day | 📋 PLANNED | Background service polls Inbox at the SyncInterval, asks a local Ollama model (e.g. `llama3.1:8b` or `qwen2.5:7b`) to score importance / category. Writes a per-message metadata sidecar (priority 1-5, suggested folder, summary). UI surfaces priority badge in mail list + a "Triage" view. |
| P3-AI-3 | **AI-suggested replies** — Ollama drafts a reply the user can edit | 📋 PLANNED | Reply button gets an AI dropdown alongside the regular send. Draft sits in the compose window for review — never auto-sent. |
| P3-AI-4 | **AI summarization** — long thread → 3-bullet summary in reading pane | 📋 PLANNED | Lazy: only when user clicks "Summarize". Cached per message id. |
| P3-AI-5 | **Privacy-first AI settings** — Ollama endpoint / model picker / opt-in per feature | 📋 PLANNED | All AI off by default. Endpoint defaults to `http://localhost:11434`. Each feature has its own toggle. No data leaves the LAN unless user explicitly points at a remote endpoint. |
| P3-AI-6 | **Semantic search** — Ollama embeddings over indexed bodies | 📋 PLANNED | Builds on P1-5b. "Find emails about the office relocation" hits messages that don't literally contain those words. Off by default; opt-in. Embedding cache lives next to the FTS index. |
| P3-AI-7 | **Auto-classify ads / marketing / transactional** — train a tiny local classifier from the user's read-frequency signal | 📋 PLANNED | Feeds into search-scope demotion (P1-5d) and notification suppression (P1-7b). Self-improving as the user reads more. Reset / retrain button in Settings. |

---

## Search & Index Strategy (deep dive)

This section drives the order and constraints for P1-5*, P2-16, P2-17, and the AI search items.

### Principles
1. **Strict by default.** Basic search matches **only** sender display name, sender email, and subject. The user has to opt into body search via Advanced Search. Empty queries return all messages — no surprise hidden filtering.
2. **No syntax to memorize.** The advanced search is a builder UI, not a query language. The phrase `from:foo subject:bar` belongs to other clients; ours uses fields and toggles.
3. **Partial match by default; "Exact" is a toggle, not a quote convention.** Quotes in the search box mean a literal substring containing a quote — they are not a query operator.
4. **Disk size is not a constraint.** Index everything the user is likely to search. Cleanup is opt-in (P2-17), not automatic.
5. **Indexing happens off the UI thread; results stream.** A search returning 50,000 messages must show the first hundred immediately and keep filling.
6. **Junk and learned-noise folders are excluded from default scope.** "Include all" toggle reveals them. This is the answer to "no more 8 million false positives when I search for sentrix."
7. **Search never modifies server state.** Read flags, flagging, etc. flow only from explicit user actions, not from search-touch.

### Phases
- **Phase 1 (shipped v0.12.0):** strict in-folder basic search across sender / email / subject. No body, no index. Filters in-memory off the already-loaded message list.
- **Phase 2 (P1-5b):** local FTS index over message bodies + headers. SQLite FTS5 is the default candidate (fits the existing SQLite cache, no external dependency); Lucene.NET is the fallback if FTS5 ranking proves insufficient. Index updates incrementally on poll. Rebuildable from the message cache (P2-16) without re-fetching from IMAP.
- **Phase 3 (P1-5a):** Advanced Search builder dialog, opens from the toolbar. Field rows AND together; an "Any of these" group switches to OR. Each row has a per-field Exact toggle.
- **Phase 4 (P1-5d, P1-5e, P3-AI-7):** read-frequency learning + folder demotion. Default scope filters out folders the user almost never opens, junk, and a learned classifier's "marketing" bucket.
- **Phase 5 (P3-AI-6):** semantic search over the same index, gated behind P3-AI-5 privacy toggles.

### Storage layout (target)
- `%LOCALAPPDATA%\GreatEmailApp\cache.db` — existing message envelope/body cache (P2-16 expands its retention).
- `%LOCALAPPDATA%\GreatEmailApp\search.db` — FTS5 virtual table over `(account_id, folder_path, uid, subject, sender_name, sender_email, body_text, received_at)`. Plus `search_meta` for read-frequency counters. Local-only; never synced.
- Both DBs honor P2-17 cleanup actions atomically.

### Non-goals
- Cross-machine search index sync. Indexes are per-PC by design — the local cost is high and the win is small (cloud snapshot already syncs accounts/settings).
- Server-side IMAP SEARCH as the primary path. Used only as a last-resort fallback when the local index is missing data (e.g. during initial backfill). IMAP SEARCH is too slow and too server-dependent to drive a UX promise.

---

## Recommended Execution Order

### Phase 1 — Shell (this sprint)
P0-1, P0-2, P0-3 — visuals match the design with dummy data.

### Phase 2 — IMAP (next)
P0-4 → P0-8 — accounts can be added, mail flows.

### Phase 3 — Settings
P0-9, P0-10 — user can configure everything.

### Phase 4 — Firebase sync
P0-11 → P0-13 — multi-PC parity.

### Phase 5 — P1 polish
Compose, search, notifications, HTML rendering.

---

## Decision Log

| Decision | Context | Made By | Date | Status |
|----------|---------|---------|------|--------|
| Stack: WPF + .NET 8 vs Electron | Windows-native look, MailKit polish, smaller install | James Reed | 2026-04-29 | DECIDED |
| Passwords stay local (WCM), settings sync via Firebase | Avoids storing creds in cloud; passwords don't leave the IMAP transaction | James Reed | 2026-04-29 | DECIDED |
| Firebase Auth via Google sign-in only (v1) | Simplest OAuth path; no user-management surface to maintain | James Reed | 2026-04-29 | DECIDED |
| Last-write-wins for settings sync conflicts | Single user across multiple PCs; CRDT overkill | James Reed | 2026-04-29 | DECIDED |
| Ribbon style: pro/Outlook-like (vs flat toolbar) | User preference for traditional Outlook look | James Reed | 2026-04-29 | DECIDED |
| **Encrypted password sync via Firestore** (reverses original "passwords stay local, period") | Re-typing 6–7 IMAP passwords on every new PC was real friction. Reviewed the threat model: an IMAP password unlocks data already exposed by any host-side breach, so "absolute zero cloud touch" was the wrong proportional choice. Mitigation: per-user random data key, wrapped by an Argon2id-derived KEK from a user-chosen master passphrase; AES-256-GCM for both wrap and per-account ciphertexts; separate `users/{uid}/vault/passwords` Firestore doc; unwrapped data key DPAPI-cached locally so the passphrase prompts only on first PC + on resync. See rulebook §7C. | James Reed | 2026-05-07 | DECIDED |

---

## Known Tech Debt Backlog

| Item | Why Deferred | Target |
|------|-------------|--------|
| _(none yet)_ | — | — |

---

## Site / App Health Tracker

| Category | Count | Notes |
|----------|-------|-------|
| 🔴 Critical | 0 | |
| 🟠 High | 0 | |
| 🟡 Medium | 0 | |
| ⚪ Low | 0 | |
| **Total open** | **0** | |

---

## Competitive Analysis

| Competitor | What they do well | Our advantage |
|------------|------------------|--------------|
| Microsoft Outlook (classic) | Mature ribbon, calendar integration, Exchange | We're focused: email only, no bloat, free, IMAP-first |
| Mozilla Thunderbird | OSS, extensible, IMAP-native | We're more polished, Windows-native, ribbon UX |
| Mailbird | Modern look, multi-account | We sync settings via Firebase, free, no subscription |
| eM Client | Pro features | We're focused on a single use case, lighter |

---

## What's New (customer-facing changelog)

Customer-readable release notes. Newest first. Surfaced in **Settings → About → What's new** and on the GitHub Releases page. Internal `FIX-YYYY-MM-DD-NNN` IDs map to entries in `Project/logs/fix_log.md` for engineering context — this section is the user-friendly view.

> **Maintenance rule:** every release that ships a user-visible change adds a row here **before** the build is published. Pure internal refactors with no user impact may be omitted. Keep entries short, plain-English, and free of file paths or class names.

### v0.12.5 — 2026-05-15
**What's new**
- **Built-in spam filter.** Auto-classifies incoming mail and quietly routes spam to the Junk folder, marking it read so the unread badge stays clean. Ships with ~70 default keyword patterns across pharma / lottery / phishing / investor cold-outreach / business-acquisition / overseas-manufacturer pitches / mailbox-quota fraud. Per-account trusted-sender list bootstraps from everyone you've ever emailed (your Sent folder) so legitimate correspondents are never caught. Config lives at `%LOCALAPPDATA%\GreatEmailApp\spam-filter.json` — Settings UI coming in a later release.
- **"Search server for more results."** Outlook-style link at the bottom of the message list. Type in the search box; if local results miss something (or the message is in a different folder), click the link and the app runs an IMAP SEARCH across every folder of the current account. Clearing the search restores the normal view.
- **Auto-refreshing message list.** When new mail arrives in the background, the visible list now updates in place instead of waiting for you to click a folder to refresh.

**Bug fixes**
- Fixed: the **new-mail notifications toggle** silently disabled the entire polling subsystem when turned off — not just balloons. With it off, the search index, future spam filter, mail rules, and auto-refresh all went dormant. Toggle now only controls the balloon; polling always runs while the app is open.
- Fixed: the message list briefly showed weeks-old mail when you re-opened the Inbox. The local cache was sorting display strings alphabetically by day-of-week ("Wed" > "Tue" > "Thu"), surfacing Tuesday-dated mail above today's mail. Now uses proper chronological order — the cache also self-heals old entries on the next sync.
- Fixed: clicking a folder doesn't always scroll back to the top. The mail list now resets to the newest message on every folder switch.
- Fixed: the reading pane briefly went blank when a background refresh fired while you were reading a message. The current message's body is now preserved across refreshes.
- Fixed: Settings → Send/Receive said "0 = manual only" but the app silently treated 0 as 1 minute. Description corrected and value clamped to ≥ 1.

**Polish**
- Search box placeholder text now disappears the moment you click into the box, not just on the first keystroke — so you can tell whether the search box has focus.
- Selected message in the list now has a clear accent border so it's visually distinct from an unread row (they used to share the same tint).
- Junk folder unread badge is hidden by default — flip on in Settings if you want to see how much was caught.
- Multi-account setups deterministically land on the first account's inbox at startup instead of racing on whichever IMAP server answered first.
- Sync chip tooltip now wraps cleanly and includes a green/amber/red legend so the chip's color always tells the whole story.

### v0.12.1 — 2026-05-10
**Bug fixes**
- Fixed: the **new-mail notifications toggle** in Settings → Notifications wasn't syncing across PCs. The HTML rendering and remote-images security toggles already synced correctly, but the notification toggle was being pushed to the cloud and silently dropped when the other PC pulled. All settings now share a single sync path so future toggles can't be forgotten.


**Bug fixes**
- Fixed: read vs unread messages now look obviously different — unread rows have a tinted background and bright bold text; read rows are dim and regular weight.
- Fixed: filter pills (All / Unread / Flagged / Mentions) no longer get stuck with two highlighted at once. Clicking one clears the others.

**What's new**
- The **search box** above the message list now actually searches. Strict matching against **sender name, sender email, and subject only** — no body matching, so searching for "sentrix" no longer hits thousands of newsletter false positives. Case-insensitive partial match. Combines with the filter pills (e.g. *Unread + "invoice"*).
- The bigger picture: see the new **Search & Index Strategy** section in `roadmap.md`. Advanced search builder, local full-text index, junk/marketing demotion, and read-frequency learning are all on the way.

### v0.11.12 — 2026-05-10
**Bug fixes**
- Fixed: the **All / Unread / Flagged** filter pills above the message list looked clickable but didn't actually filter anything. They now hide read or unflagged messages as expected. *Mentions* is a pass-through until that feature lands.

### v0.11.11 — 2026-05-10
**Bug fixes**
- Fixed: app would sometimes crash on first launch and only run on the second try. *(FIX-2026-05-10-002)*
- Fixed: a brief startup race where new-mail notifications could crash the app instead of showing the balloon. *(FIX-2026-05-10-002)*

### v0.11.10 — 2026-05-10
**What's new**
- Folder unread badges restyled in vibrant orange-red on white for higher contrast in the sidebar.

### v0.11.9 — 2026-05-10
**What's new**
- Folder unread badges are tighter, bolder, and easier to read at a glance.

### v0.11.8 — 2026-05-10
**Bug fixes**
- Fixed: leaving the app idle for a few minutes would clear the message list, the preview pane, and the selected folder when you came back. The app now stays exactly where you left it.

### v0.11.7 — 2026-05-10
**What's new**
- Sidebar: subfolders are now collapsed by default. Click the chevron next to a parent folder to expand its children. Stops the sidebar from blowing past screen height when you have a heavily-foldered account.
- Right-click → **Move to**: subfolders now nest under their parent as a real submenu (hover to open) instead of a flat indented list that ran off the screen. The parent folder remains directly clickable to move into the parent itself.

### v0.11.6 — 2026-05-10
**Bug fixes**
- Same sync fix as v0.11.5 — re-released so the in-app updater offered the fix to PCs already on a build labelled 0.11.5.

### v0.11.5 — 2026-05-10
**Bug fixes**
- **Major sync fix.** Account lists were silently being overwritten between PCs: clicking *Sync now* claimed success but actually pushed the local roster to the cloud, clobbering accounts that another PC had pushed. Sync now correctly applies remote changes when the cloud has new data and pushes only when there are real local edits. *(FIX-2026-05-10-001)*

  > **If you lost accounts on a PC**: install 0.11.5+ on **both** PCs. On the PC that has the full account list, open Settings → Sync → Sync now (push). Then on the other PC, do the same (pull). Order matters.

---

## Shipped Log (engineering)

Internal milestone log — feature ships rolled into the master roadmap. The customer-facing notes live in **What's New** above.

| ID | Feature | Shipped | Notes |
|----|---------|---------|-------|
| _(none yet)_ | — | — | — |
