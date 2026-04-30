# The Great Email App — DEVELOPMENT RULEBOOK
**Version:** 1.0 · Revision 1
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
- **Never sent to Firestore. Never written to settings.json. Never logged.**
- "Test Connection" button validates before saving

**Universal rules:**
- Never log credentials, tokens, or auth headers — `LogSanitizer` strips known fields
- Refresh tokens invalidated on sign-out
- IMAP password re-prompt on each new PC (passwords don't sync)

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

## 18. Lessons Learned

| Date | Category | Lesson |
|------|----------|--------|
| _(none yet)_ | — | — |
