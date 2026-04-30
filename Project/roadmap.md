# The Great Email App — Master Roadmap
**Created:** 2026-04-29 | **Updated:** 2026-04-30
**Stack:** WPF + .NET 8 (C#) + MailKit + SQLite + Firebase
**Owner:** James Reed (coolman0804@outlook.com)
**Vision:** A clean, fast, native-Windows IMAP email client with Outlook's familiar ribbon UX, dark/light theming, and Firebase-backed settings sync across multiple PCs.

---

## Vision Statement

The Great Email App (TGEA) is a focused desktop email client for power users who run multiple IMAP accounts and want Outlook's familiar ribbon-and-folder layout without the bloat of calendar/tasks/teams. Settings sync via Firebase so installing on a new PC restores all account configs (passwords stay local, in Windows Credential Manager). Built for fiksdit.com IMAP first, but works with any IMAP server.

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
| P0-1 | WPF shell — title bar, ribbon, three-pane layout, status bar | ✅ DONE | Phase 1 |
| P0-2 | Light/dark theme + accent color, tokens from design | ✅ DONE | Phase 1 |
| P0-3 | Resizable panes (sidebar 200–380, mail list 300–560) | ✅ DONE | Phase 1 |
| P0-4 | Add Account dialog with IMAP/SMTP fields + Test Connection | ✅ DONE | Phase 2 |
| P0-5 | Windows Credential Manager integration for passwords | ✅ DONE | Phase 2 |
| P0-6 | IMAP connection (MailKit) — list folders, fetch messages | ✅ DONE | Phase 2 |
| P0-7 | Local SQLite cache for messages | 📋 PLANNED | Phase 2 |
| P0-8 | Send mail via SMTP | 📋 PLANNED | Phase 2 |
| P0-9 | Settings dialog (General, Accounts, Appearance, Sync) | ✅ DONE | Phase 3 |
| P0-10 | Settings persistence (`settings.json` in `%LOCALAPPDATA%`) | ✅ DONE | Phase 3 |
| P0-11 | Google sign-in (Firebase Auth) | ✅ DONE | Phase 4 — FirebaseAuthService (Google OAuth2 loopback + Firebase REST) |
| P0-12 | Firestore settings sync (push/pull on change + on launch) | ✅ DONE | Phase 4 — FirestoreSyncService (REST API); push on save, pull on startup |
| P0-13 | First-run sign-in screen with skip path | ✅ DONE | Phase 4 — FirstRunOverlay shown on first launch; skippable |

**Status key:** `📋 PLANNED` · `🔧 IN PROGRESS` · `⚠️ PARTIAL` · `✅ DONE`

---

## 🟠 P1 — High Impact

Core email workflow that makes the app actually usable.

| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P1-1 | Compose window (new email, reply, reply all, forward) | 📋 PLANNED | |
| P1-2 | Attachment handling (download, preview, attach to outgoing) | 📋 PLANNED | |
| P1-3 | Mark read/unread, flag, archive, delete | 📋 PLANNED | |
| P1-4 | Move-to-folder | 📋 PLANNED | |
| P1-5 | Search (server-side IMAP SEARCH + local cache fallback) | 📋 PLANNED | |
| P1-6 | Auto sync interval (configurable polling per account) | 📋 PLANNED | |
| P1-7 | IMAP IDLE for real-time push where supported | 📋 PLANNED | |
| P1-8 | New mail notifications (Windows toast) | 📋 PLANNED | |
| P1-9 | HTML email rendering with remote-image gating | 📋 PLANNED | WebView2 surface; off when Settings.ShowHtml=false |
| P1-10 | Backstage view (File tab) | 📋 PLANNED | |
| P1-11 | Keyboard shortcuts (Ctrl+R reply, Ctrl+Enter send, Del delete, F5 send/receive, Ctrl+Shift+M new) | 📋 PLANNED | |
| P1-12 | Multi-select in mail list (Ctrl+click, Shift+click) + batch archive/delete/move | 📋 PLANNED | |
| P1-13 | First-run onboarding when launched with zero accounts | 📋 PLANNED | Replaces sample data with a guided Add Account flow |
| P1-14 | App icon + branded taskbar/installer presence | 📋 PLANNED | .ico + AppxManifest fields |

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

## Shipped Log

| ID | Feature | Shipped | Notes |
|----|---------|---------|-------|
| _(none yet)_ | — | — | — |
