# The Great Email App — DEVELOPMENT RULEBOOK
**Version:** 1.0 · Revision 3
**Updated:** 2026-06-13
**Stack:** WPF + .NET 8 (C#) + MailKit (IMAP/SMTP) + SQLite (local cache) + Firebase (Auth + Firestore for settings sync)
**Authority:** Mandatory for All AI + Human Contributors
**Owner:** James Reed (coolman0804@outlook.com)

> **AI:** Read the relevant sections before writing any code. Confirm file revs before modifying. Verify schema before any DB-touching code. Check §8 Known Issues. Apply §2 Surgical Change Rule.

---

## TABLE OF CONTENTS

1. [Priorities & Meta-Protocol](#1-priorities--meta-protocol)
2. [Laws & Surgical Change Rule](#2-laws--surgical-change-rule)
3. [Folder Structure](#3-folder-structure)
4. [File Dating & Authorship](#4-file-dating--authorship)
5. [Routes & Security](#5-routes--security)
6. [Data Model](#6-data-model)
7. [Auth Flow](#7-auth-flow)
8. [Known Issues](#8-known-issues)
9. [Service Standards](#9-service-standards)
10. [Components & UI/UX](#10-components--uiux)
11. [Error Handling](#11-error-handling)
12. [Environment & Config](#12-environment--config)
13. [CCCP Protocol](#13-cccp-protocol)
14. [Query & Data Limits](#14-query--data-limits)
15. [Session Log Protocol](#15-session-log-protocol)
16. [Fix Log Protocol](#16-fix-log-protocol)
17. [Lessons Learned](#17-lessons-learned)

---

## 1. Priorities & Meta-Protocol

**Priority order:** Security > Correctness > Speed > Scalability > Maintainability

**Output format rule:**
- All files include the header block from §4 (FILE / Created / Revised / Rev / Changed by)
- Never output placeholders, stubs, TODOs, or lorem ipsum

**AI Session Start Checklist:**
- [ ] Read relevant rulebook sections for the current task
- [ ] Confirm current rev of any file being modified (read the file header)
- [ ] Check §8 Known Issues — is this already diagnosed?
- [ ] Apply §2 Surgical Change Rule — touch only what's broken
- [ ] Open or create today's session log — `Project/sessions/session-YYYY-MM-DD.md` (see §15)

**AI Session — Ongoing:**
- After every non-trivial fix, discovery, or decision → append one line to the session log immediately
- After any bug is resolved → append to `Project/logs/fix_log.md`
- Do NOT wait until end of session — context compaction wipes chat memory

**AI Session End:**
- [ ] Flush session log
- [ ] Run EOD skill to generate `session-summary-YYYY-MM-DD.md`
- [ ] Commit all changes

**Stop and Verify Rule:** If a fix doesn't work on the first attempt, do NOT try a second variation. Verify the component is actually loaded, verify the file you edited is referenced, state what you found before proposing the next fix. Three failed attempts = stop and ask the user.

---

## 2. Laws & Surgical Change Rule

- No placeholders, stubs, TODOs, or lorem ipsum — ever
- Read existing code before generating anything
- All files must include creation/revision date + rev number at top
- Ask before creating any new folder, namespace, control, service, model, or migration
- No new NuGet packages without explicit owner approval

### DOCUMENT YOUR CHANGES IN THE FILE — NO SILENT MODIFICATIONS

Any non-trivial change — a limit, a default, a workaround, a special case — gets documented in that file's header comment at the time of the change. Not in chat. In the file.

```csharp
// NOTE: fetch limit set to 200 per §14 — IMAP servers throttle on larger initial pulls
// NOTE: STARTTLS forced on port 143 — fiksdit.com mail server requires it
```

**Rule in one sentence:** If a future developer or AI session would need to read a chat log to understand why you did something, you didn't document it enough.

### THE MOST IMPORTANT LAW — SURGICAL CHANGE RULE

> When the user says something is working and only X needs fixing: **change ONLY X.** Not the imports. Not the types. Not the flow. Not the naming. **ONLY the specific broken thing.**
>
> Rewriting working code to "clean it up" while fixing a bug is **forbidden.** Read the error message. Fix only what the error describes.

---

## 3. Folder Structure

```
The Great Email App/
├── src/
│   ├── GreatEmailApp/                  # WPF app (UI layer)
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MainWindow.xaml / .cs
│   │   ├── Views/                      # Windows, dialogs, pages
│   │   │   ├── Backstage/
│   │   │   ├── Dialogs/                # AddAccount, Settings, SignIn
│   │   │   └── Panes/                  # Sidebar, MailList, Reading
│   │   ├── Controls/                   # Custom controls (Ribbon, TitleBar, etc.)
│   │   ├── ViewModels/                 # MVVM view models
│   │   ├── Themes/                     # Light.xaml, Dark.xaml, Brushes.xaml
│   │   ├── Converters/                 # IValueConverter implementations
│   │   ├── Resources/                  # Icons, fonts, images
│   │   └── GreatEmailApp.csproj
│   ├── GreatEmailApp.Core/             # Logic / models / services (no WPF deps)
│   │   ├── Models/                     # Account, Folder, Message, Settings
│   │   ├── Services/                   # ImapService, SyncService, SettingsStore
│   │   ├── Storage/                    # SQLite cache, JSON settings
│   │   └── GreatEmailApp.Core.csproj
│   └── GreatEmailApp.sln
├── docs/
│   └── design/                         # Original Claude Design mockup (HTML/JSX)
├── Project/
│   ├── rulebook.md                     # THIS FILE
│   ├── roadmap.md
│   ├── sessions/                       # gitignored daily logs
│   └── logs/
│       └── fix_log.md                  # committed
├── .claude/
│   └── skills/                         # in-project skills
├── project-guidelines/                 # original templates (kept for reference)
└── README.md
```

**Universal rules:**
- Config/env files at project root — never inside `src/`
- Shared utilities in `GreatEmailApp.Core/` — never duplicated
- No business logic in Views — keep XAML/code-behind presentational; use ViewModels
- All ViewModels in `ViewModels/` — never inline classes inside Views
- Tests (when added) live in `tests/GreatEmailApp.Tests/`

---

## 4. File Dating & Authorship

Every generated or modified file (C#, XAML, MD) must include at the top:

```csharp
// FILE: src/GreatEmailApp/Controls/Ribbon.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
```

For XAML, use:
```xml
<!-- FILE: src/GreatEmailApp/Controls/Ribbon.xaml
     Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
     Changed by: Claude Opus 4.7 on behalf of James Reed -->
```

**Authored-By Rule:**
- Update the line on subsequent edits — do not stack multiple lines
- Use the actual model name (e.g. `Claude Opus 4.7`) — never just "Claude"

**Non-Obvious Changes Must Be In-File:** every limit, workaround, or default value gets a comment at the line.

---

## 5. Routes & Security

This is a desktop app — no web routes. Security boundaries instead:

| Boundary | Rule |
|----------|------|
| IMAP / SMTP credentials | Stored in **Windows Credential Manager only** — never in Firestore, never in plaintext on disk |
| Firebase auth tokens | Stored in `%LOCALAPPDATA%\GreatEmailApp\auth.dat` encrypted via DPAPI (per-user) |
| Settings synced to Firestore | Account configs, UI prefs, rules — **passwords are never in this set** |
| Firestore security rules | `allow read, write: if request.auth.uid == userId` — settings doc keyed by UID |
| TLS | All IMAP/SMTP connections use TLS or STARTTLS — plaintext refused unless user explicitly enables in advanced settings |
| External URLs in emails | HTML reading pane disables remote image loading by default; user toggles per-sender |

---

## 6. Data Model

**Storage layers:**
- **Windows Credential Manager** — IMAP/SMTP passwords (per account)
- **Local SQLite** — `%LOCALAPPDATA%\GreatEmailApp\cache.db` — message metadata, folder state, offline cache
- **Local JSON** — `%LOCALAPPDATA%\GreatEmailApp\settings.json` — UI prefs (theme, pane widths, accent)
- **Firestore** (when sync enabled) — collection `users/{uid}/settings` — synced subset of settings + account configs (no passwords)

### Core entities (sketched — finalize in Phase 2)

| Entity | Key fields |
|--------|-----------|
| `Account` | id (GUID), displayName, emailAddress, imapHost, imapPort, imapEncryption, smtpHost, smtpPort, smtpEncryption, username, color, status |
| `Folder` | id, accountId, name, fullPath, specialUse (Inbox/Sent/etc), unreadCount, totalCount |
| `Message` | uid, folderId, accountId, sender, senderEmail, subject, preview, date, flags (read/flagged/answered), hasAttachments |
| `Settings` | theme, accent, ribbonStyle, density, paneWidths, syncEnabled |

**Rules:**
- Soft-delete only — never hard-delete a cached message; mark `is_deleted_local`
- All tables get `created_at` and `updated_at`
- Schema changes via migration files only (in `GreatEmailApp.Core/Storage/Migrations/`)
- All SQLite queries parameterized — never string concatenation

---

## 7. Auth Flow

**Two distinct auth surfaces:**

### A) Firebase Auth (for settings sync)
- **Provider:** Google sign-in only (v1)
- **Flow:** desktop OAuth — app opens default browser → user authenticates at accounts.google.com → callback to localhost listener → ID token + refresh token returned
- **Storage:** refresh token encrypted via DPAPI in `%LOCALAPPDATA%\GreatEmailApp\auth.dat`
- **Scope:** Firestore read/write to `users/{uid}/settings` only
- **Skip path:** user can dismiss sign-in and run app fully locally

### B) IMAP / SMTP (for email)
- Plain username + password (or app passwords where supported)
- Password entered in Add Account dialog → stored in Windows Credential Manager keyed by account ID
- **Plaintext is local-only** — never written to settings.json, never logged.

### C) Password sync vault (added 2026-05-07)

The original §7B rule "passwords never leave the device" was reversed after a
review of the threat model: re-typing 6–7 IMAP passwords on every new PC was a
real friction point, and the data being protected (IMAP passwords) is no more
sensitive than the email contents already accessible via any IMAP-host breach.
See decision log entry "Encrypted password sync via Firestore (2026-05-07)".

**The vault** lives at `users/{uid}/vault/passwords` in Firestore — a separate
document from the regular settings sync, so settings churn doesn't touch it.

**Crypto pipeline (see `Core/Crypto/PasswordVault.cs`):**

```
master passphrase  --[Argon2id, 64MiB, t=3, p=4]-->  KEK (32B)
random data key (32B)  --[AES-256-GCM, key=KEK]-->  wrapped data key  ──► Firestore
imap password (per account)  --[AES-256-GCM, key=data key, AAD=accountId]-->  ct  ──► Firestore
```

**Why a wrapped data key (not deriving the encryption key directly from the
passphrase):** lets the user change the passphrase without re-encrypting every
password — just rewrap the data key. Also keeps Argon2id off the hot path for
add-account / change-password operations.

**Local cache** (`%LOCALAPPDATA%\GreatEmailApp\vault.dat`): the **unwrapped data
key** is DPAPI-encrypted (CurrentUser scope) once the user enters the passphrase
on a PC, so the passphrase prompts only on first-time setup or explicit "Resync
passwords." See `Core/Crypto/LocalDataKeyCache.cs`.

**Threat model boundary:** Firebase / GCP at-rest encryption + Firestore rules
+ AES-GCM + Argon2id KDF. A Firebase compromise alone does not leak IMAP
passwords (attacker would still need the passphrase). Loss of the master
passphrase is unrecoverable — the user re-enters IMAP passwords manually.

**Rules:**
- Plaintext IMAP passwords still never live in settings.json or in any non-vault
  Firestore field. The Credential Manager remains the only on-device store.
- Vault uploads only happen when the vault is **unlocked** on a PC. If locked,
  add-account silently skips the vault push and the password lands in the cloud
  on the next "Resync passwords."
- The unwrapped data key never leaves the device. The wrapped key, the salt,
  and per-account ciphertexts are the only things that travel.
- Never log the passphrase, the KEK, the data key, or any plaintext password.
  `LogSanitizer` strips known fields.

**Universal rules (all auth surfaces):**
- Never log credentials, tokens, or auth headers — `LogSanitizer` strips known fields
- Refresh tokens invalidated on sign-out
- New PC: sign in with Google → unlock vault with master passphrase → IMAP
  passwords restore to Credential Manager automatically. No re-entry.

---

## 8. Known Issues

| # | Priority | Description | Status |
|---|----------|-------------|--------|
| _none yet_ | — | — | — |

---

## 9. Service Standards

Every service method that talks to IMAP, Firestore, or SQLite follows this pattern:

**Return shape — discriminated result:**
```csharp
public abstract record Result<T>
{
    public sealed record Ok(T Value) : Result<T>;
    public sealed record Fail(string Error, Exception? Inner = null) : Result<T>;
}
```

**Mandatory checklist per service method:**
- [ ] Validate inputs at the top — fail fast with `Result.Fail`
- [ ] Wrap external calls (IMAP, HTTP, DB) in try/catch
- [ ] Log with structured context: `_logger.LogError(ex, "[{Method}] {Detail}", nameof(Method), detail)`
- [ ] Return typed `Result<T>` — never throw across service boundaries
- [ ] List queries: explicit limit (see §14)
- [ ] Async only — no `.Result` or `.Wait()`, ever

---

## 10. Components & UI/UX

**Pattern:** WPF + MVVM (light — using `CommunityToolkit.Mvvm` for `ObservableObject`/`RelayCommand`).

**Design tokens:** all colors, radii, spacing live in `Themes/Brushes.xaml` (light) and `Themes/Brushes.Dark.xaml` (dark). Tokens mirror the design's CSS custom properties (see `docs/design/styles.css`).

**Universal rules:**
- No business logic in code-behind beyond view-only concerns (focus, drag handles, etc.)
- No data access in Views — go through ViewModels and services
- Every async UI action: loading state + error toast + cancellable where applicable
- Empty states: every list has a designed empty state (no folders, no emails, no accounts)
- Never hardcode colors — use `{DynamicResource ...}` brushes
- All interactive elements: `AutomationProperties.Name` for accessibility

### WPF conventions established in this codebase

**Click handlers on non-Button elements** (Border, ItemsControl rows, etc.):
- Use `MouseLeftButtonDown="Handler"` + `Tag="{Binding}"` on the Border
- In code-behind: `if (sender is FrameworkElement fe && fe.Tag is FooViewModel foo) { … }`
- Don't use Commands on a Border — Border isn't ButtonBase. Use Commands only on Button/ToggleButton/MenuItem/Hyperlink.

**Caret-vs-row click resolution** (sidebar folder + account headers):
- Caret is a child element of the row Border. Both have `MouseLeftButtonDown` handlers (bubbling).
- Caret handler runs FIRST (innermost), sets `e.Handled = true` to prevent row selection.
- This is why we use bubbling, not tunneling (`PreviewMouseLeftButtonDown`) — preview tunnels parent→child, which would fire row first and steal the event.

**Account header is the exception** — uses `PreviewMouseLeftButtonDown` because there's no inner element to coordinate with; the Preview just makes sure the event always fires.

**Context menus** — code-behind pattern:
- Define `<ContextMenu>` inline in the Border. Each `<MenuItem>` has a `Click="..."` handler.
- Resolve the target VM by walking up: `sender (MenuItem) → ContextMenu (via parent) → ContextMenu.PlacementTarget (the right-clicked Border) → .Tag (the VM)`.
- For dynamic submenus (e.g. Move To… listing every folder), populate in the `Opened` event.
- Always `vm.SelectXxxCommand.Execute(target)` on right-click first so the action is visibly applied to the right item.

**Theme references:**
- `{DynamicResource ...}` for anything that swaps with theme (brushes, popups). Mandatory for live theme changes to work.
- `{StaticResource ...}` for sizes, layout doubles, icon glyph strings, fonts, converters — values that don't change at runtime.

**`{x:Static enum}` in `ComboBoxItem.Tag`** is fragile across XAML init ordering. **Don't.** Use the established pattern instead:
- VM exposes `IReadOnlyList<TOption>` of records like `(MailEncryption Value, string Label)`.
- ComboBox: `ItemsSource="{Binding Options}"`, `DisplayMemberPath="Label"`, `SelectedItem="{Binding SelectedOption, Mode=TwoWay}"`.
- A wrapper property on the VM mirrors `SelectedOption.Value` to the underlying enum field.

**`DataTemplate.Triggers`** must be a direct child of `<DataTemplate>`, not nested in any element inside it. Common XAML compile-time trap.

**Recursive data templates** (folder tree): define the `<DataTemplate x:Key="FooTemplate">` at the UserControl level, then reference it inside itself via `ItemTemplate="{DynamicResource FooTemplate}"` on the inner ItemsControl. Use `DynamicResource`, not `StaticResource`, so the lookup happens at runtime.

**Custom ComboBox / ContextMenu / MenuItem** need a full `ControlTemplate` to look right in dark mode — WPF defaults render with system colors and break theming. Templates live in `Themes/Controls.xaml`.

---

## 11. Error Handling

**Hierarchy:**
1. Validate inputs before the operation
2. Catch errors at the service layer — never let them propagate raw to the UI
3. Log with context using `Microsoft.Extensions.Logging`
4. Return structured `Result<T>` to the caller
5. Display user-friendly messages — never show raw exception text or stack traces in production UI

**Never:**
- Swallow errors silently (`catch {}` empty)
- Show raw IMAP server text in dialogs
- Retry network failures without delay + max attempts (exponential backoff, max 5)

---

## 12. Environment & Config

**No `.env` files** — this is a desktop app. Config sources, in priority order:
1. Command-line args (`--theme dark`, `--reset-settings`)
2. `%LOCALAPPDATA%\GreatEmailApp\settings.json`
3. Built-in defaults (compiled into `Defaults.cs`)

**Build-time config** (in `GreatEmailApp/appsettings.json`, committed):
- Firebase project ID, public API key (these are public per Firebase model)
- OAuth client ID
- Logging levels per namespace

**Secrets that must NEVER appear in source:**
- Firebase service account keys (server-side only — we don't ship one)
- Any IMAP/SMTP credential
- Any user's OAuth refresh token

| Variable | Required | Description |
|----------|----------|-------------|
| `FIREBASE_PROJECT_ID` | Yes (build) | Firebase project for Firestore sync |
| `GOOGLE_OAUTH_CLIENT_ID` | Yes (build) | Desktop OAuth client ID |

---

## 13. CCCP Protocol

Not used for this project — we are using direct file edits via Edit/Write tools. CCCP retained as reference only.

---

## 14. Query & Data Limits

**Mandatory:** every list query (SQLite or IMAP) has an explicit limit.

| Use Case | Limit |
|----------|-------|
| Initial folder open (mail list) | 200 messages |
| Older messages on scroll | +200 per page |
| Folder list per account | No limit (typically <100) |
| Search results | 500 max |
| Settings document size (Firestore) | 1 MB hard cap — enforce in `SyncService` |

Document inline:
```csharp
.Take(200) // §14 — initial folder page; older loaded on scroll
```

---

## 15. Session Log Protocol

**File:** `Project/sessions/session-YYYY-MM-DD.md`
**Gitignored:** Yes
**Format:** one line per event, `HH:MM  TYPE  detail`

**Rules:**
- Open or create the log at session start
- Append after every meaningful unit of work
- Never batch at end of session

---

## 16. Fix Log Protocol

**File:** `Project/logs/fix_log.md`
**Gitignored:** No — committed.
**Format:** see template `project-guidelines/fix-log-TEMPLATE.md`. Every bug-class change gets a structured entry. The "Tried" field is mandatory.

---

## 17. Rendering

**WPF process render mode is `SoftwareOnly`** — set in `App.OnStartup` before `base.OnStartup(e)`.

Hardware-accelerated WPF on certain GPU/driver combinations produces pure-white windows even though the visual tree, theme dictionaries, and `DynamicResource` lookups all succeed. See FIX-2026-04-30-001 in the fix log.

**Rule:** don't change `ProcessRenderMode` without testing on every supported PC. Software rasterization is plenty fast for our UI.

---

## 18. Search Quality (Product Pillar)

Search is a **first-class product pillar**, not a feature. The vision statement in `Project/roadmap.md` commits the app to "the world's best email search" and "important mail surfaced fast." Every change in the search/notification pathway is held to that bar. The detailed phasing lives in roadmap §"Search & Index Strategy"; the rules below are inviolable.

### 18.1 Strict by default

- **Basic search matches only sender display name, sender email, and subject. Never the body.** False positives are worse than false negatives in basic search — the user is hunting for a known sender or topic, not exploring.
- Body-content search lives behind the **Advanced Search** dialog (P1-5a) where the user has explicitly opted in.
- Empty query = show all messages. Never silently apply a hidden filter.

### 18.2 No query syntax

- Users do **not** type `from:foo subject:"bar" before:2024-01-01`. We have a builder UI instead. Anyone tempted to add a query-language parser must justify why it's better than another field row in the builder.
- Quotes in the search box are **literal characters**, not phrase delimiters. Case-insensitive partial-match is the default; "Exact" is a per-field toggle in the builder.

### 18.3 Index is local, abundant, and rebuildable

- The full-text index lives in `%LOCALAPPDATA%\GreatEmailApp\search.db` (or whatever P1-5b lands on). Not synced. Per-PC.
- Disk usage is **not a constraint**. Don't trim the index to save MB. Do support explicit user-driven cleanup (P2-17).
- Index updates happen off the UI thread. The UI never blocks on indexing.
- Index must be **rebuildable from the message cache** without re-fetching from IMAP. If the schema changes, ship a one-time rebuild on first launch of the new version.

### 18.4 Default scope demotes noise, not deletes it

- Junk, ads, and folders the user almost never opens are **excluded from default search scope** (P1-5d).
- Demotion ≠ deletion. The "Include all" toggle reveals everything. Never lie to the user about what's there.
- The signal driving demotion comes from the read-frequency learner (P1-5e). It's never hand-curated heuristics.

### 18.5 Notifications are gated by the same signal

- New-mail notifications fire **immediately** for senders/threads with positive read-frequency signal (P1-7b).
- Marketing/ads/auto-replies are silently delivered without a balloon — same demotion signal as search scope.
- The user always has explicit override lists ("always notify," "never notify").

### 18.6 Search and notifications never modify server state

- A search-touch never marks read, never unflags. Read state flips only from explicit user actions (open, mark read, mark unread).
- A notification never auto-archives or auto-flags. The user opens the message; the auto-mark-read delay then applies as usual.

### 18.7 IMAP SEARCH is fallback only

- Local index is the primary path for every user-facing search.
- Server-side IMAP SEARCH is used **only** as backfill when the local index is incomplete (initial sync, just-rebuilt index). It is never the user's wait path.

### 18.8 What "the world's best" means here

- A query returning 50,000 hits shows the first hundred **in under 200 ms** and keeps streaming.
- Re-running the same query is **instant** (cached result set, invalidated only on new mail).
- Zero-result queries surface a one-line "Searched [N] messages across [M] folders. Try Advanced Search to include body" suggestion.
- Cross-account "all mail" search is a single toggle, not three different code paths.

Any deviation from §18 needs an entry in the Decision Log of `roadmap.md` plus owner sign-off.

---

## 19. Lessons Learned

| Date | Category | Lesson |
|------|----------|--------|
| 2026-05-15 | Pipeline coupling | Never gate background pipelines (poller, indexer, rules engine) on a UI delivery toggle. `EnableNewMailNotifications` was conflated with the entire poll cycle — when the user turned it off to silence balloons, the search indexer, spam filter, rules engine, and mail-list auto-refresh all went silent too. Subscribers should opt into events; the toggle should only suppress the consumer (`TrayNotifier`), not the producer. |
| 2026-05-15 | SQLite sort columns | Any column hit by `ORDER BY` must be written in a sortable representation. SqliteMessageCache stored `m.FullTime` (human-display string like "Wed, May 13, 2026, 3:22 PM") in `sent_at`, then sorted lexicographically — surfacing Tuesday-dated mail above Friday-dated mail because "Wed" > "Tue" > "Thu" alphabetically. Use ISO 8601 for dates, lowercase for names, etc. |
| 2026-05-15 | ObservableCollection rebuild | When refreshing a list by `Clear()` + re-`Add()`, any lazily-fetched state on the destroyed `*ViewModel`s is lost. The reading pane went blank during auto-refresh because the new `MessageViewModel` carried envelope fields but not `BodyHtml`/`BodyPlain` (fetched separately on first selection). Snapshot lazy-loaded state on the survivors (current selection, expanded rows, etc.) and copy it onto the corresponding rebuilt VMs. |
| 2026-05-15 | PowerShell script glyphs | Don't put non-ASCII glyphs (`✓`, `→`, `…`) in `.ps1` files unless every shell that will run them is PowerShell 7+. Windows PowerShell 5.1 reads them as mojibake and the parser dies on unrelated downstream tokens. `scripts/publish.ps1` bricked the entire release flow this way — fixed by replacing with plain ASCII (`OK`, `->`). Same principle for any tooling expected to run cross-shell. |
| 2026-06-12 | Destructive global keys need a focus guard | A window-level `Delete` shortcut must explicitly bail when an editable control has focus. Relying on the bubbling `KeyDown` order isn't enough — an *empty* TextBox doesn't mark `Delete` handled, so the key reaches the window and would nuke the selected mail while the user is editing the search box. Guard: `if (Keyboard.FocusedElement is TextBox) break;`. Prefer bubbling `KeyDown` (not `PreviewKeyDown`) for global shortcuts so editable controls get first crack at the key. |
| 2026-06-12 | Sync new config through the whole pipeline | When a new persisted config (spam-filter.json) joins the synced set, it needs four touch-points or it silently won't travel: a field in `SyncSnapshot`, push+pull marshalling in `FirestoreSyncService` (with the field added to the `updateMask`), the store wired into `SyncCoordinator` (subscribe `Saved`, include on push, apply on pull), and the store instantiated *before* the coordinator in `App.OnStartup`. Mirror the `rules_json` path exactly. Legacy docs lacking the field must deserialize to null and be skipped on apply, never overwritten with a default. |
| 2026-06-12 | Default-merge needs a removal ledger | A load-time "merge missing built-in defaults" step (spam keywords) re-adds anything the user deliberately deleted, because "absent" and "removed" look identical. Track explicit removals in a `RemovedDefaults` list the merge skips; recompute it on save as `BuiltIns ∖ currentList`. Without it, an editor that lets users delete defaults is a lie — they reappear next load. |
