# The Great Email App — Architecture

A short orientation doc so a new contributor (or future Claude session) can get from zero to productive without reading every file.

---

## What it is

A native Windows 11 IMAP email client. Three-pane Outlook-style layout (sidebar / mail list / reading pane), ribbon toolbar, dark/light theming, hierarchical folder tree, message actions (read/flag/archive/delete/move), per-message context menus, persisted settings.

**Built for:** fiksdit.com IMAP first, but works with any IMAP/SMTP server. Personal email use; not intended as an Exchange/Teams replacement.

---

## Stack

| Layer | Choice | Why |
|---|---|---|
| UI | WPF + .NET 8 (`net8.0-windows`) | Native Windows look without Electron bloat |
| MVVM | `CommunityToolkit.Mvvm` | `ObservableObject` + `[ObservableProperty]` source generator + `[RelayCommand]` |
| IMAP/SMTP | `MailKit` (4.13) | Battle-tested .NET mail library |
| Local cache (planned) | `Microsoft.Data.Sqlite` | Phase 5 — message metadata cache |
| Settings sync (planned) | Firebase Auth + Firestore | Phase 4 — Google sign-in only |
| AI features (planned) | Ollama (local LLM) | Privacy-first; never leaves the LAN |

---

## Solution layout

```
src/
├── Directory.Build.props              # shared MSBuild settings
├── GreatEmailApp.sln
├── GreatEmailApp/                     # WPF app (UI layer)
│   ├── App.xaml + App.xaml.cs         # service locator (App.Imap / Credentials / Accounts / Settings)
│   ├── MainWindow.xaml + .cs          # custom title bar (WindowStyle=None) + 3-pane layout
│   ├── Themes/
│   │   ├── Tokens.xaml                # radii / fonts / sizes — non-color tokens
│   │   ├── Light.xaml + Dark.xaml     # color brushes — keyed identically; ThemeManager swaps
│   │   ├── Icons.xaml                 # Segoe Fluent Icons codepoints
│   │   └── Controls.xaml              # custom ControlTemplates (ComboBox, ContextMenu, MenuItem, etc.)
│   ├── Services/
│   │   └── ThemeManager.cs            # runtime theme + accent swap
│   ├── Controls/                      # UserControls
│   │   ├── TitleBar.xaml              # brand + search + sync indicator + window controls
│   │   ├── Ribbon.xaml                # File/Home/Send-Receive/View/Help tab swap
│   │   ├── Sidebar.xaml               # accounts + recursive folder tree (FolderItemTemplate)
│   │   ├── MailList.xaml              # message rows + filter pills + group headers
│   │   ├── ReadingPane.xaml           # selected message header / body / actions
│   │   └── StatusBar.xaml             # connection state + zoom slider
│   ├── ViewModels/
│   │   ├── MainViewModel.cs           # owns Accounts/Messages, all message-action commands
│   │   ├── AccountViewModel.cs        # account header VM, IsExpanded
│   │   ├── FolderViewModel.cs         # recursive — has Children, IsExpanded, IsSelected
│   │   ├── MessageViewModel.cs        # row VM, IsSelected, IsFirstInGroup
│   │   ├── AddAccountViewModel.cs     # IMAP/SMTP form, auto-fill, test connection
│   │   └── SettingsViewModel.cs       # wraps AppSettings, live preview for theme/accent
│   ├── Converters/Converters.cs       # BoolToVis, NullToVis, BoolToFontWeight, HexToBrush, etc.
│   └── Views/Dialogs/
│       ├── AddAccountDialog            # modal IMAP/SMTP setup
│       └── SettingsDialog               # left-rail General/Accounts/Appearance/Sync/Notifications/Advanced
└── GreatEmailApp.Core/                # logic layer — no WPF deps
    ├── Models/
    │   ├── Account.cs                 # connection config (NO password here)
    │   ├── Folder.cs                  # has Children list — tree, not flat
    │   ├── Message.cs
    │   └── AppSettings.cs             # theme, accent, density, ribbon, ShowHtml, MarkReadDelaySeconds, SyncIntervalMinutes
    ├── Services/
    │   ├── Result.cs                  # discriminated Result<T> per rulebook §9
    │   ├── ICredentialStore + WindowsCredentialStore  # P/Invoke advapi32 — passwords here only
    │   ├── IAccountStore + JsonAccountStore           # accounts.json (no passwords)
    │   ├── ISettingsStore + JsonSettingsStore         # settings.json
    │   └── IImapService + ImapService                 # MailKit — connect, list folders/messages, fetch body, set seen/flagged, move
    └── Storage/AppPaths.cs            # %LOCALAPPDATA%\GreatEmailApp paths

docs/
├── architecture.md                    # THIS FILE
└── design/                            # original Claude Design HTML/JSX mockup

Project/
├── rulebook.md                        # mandatory dev rules — read before any task
├── roadmap.md                         # P0–P3 plan + decisions
├── sessions/                          # daily logs (gitignored)
└── logs/fix_log.md                    # persistent bug audit trail (committed)

.claude/skills/                        # in-project AI skills
.editorconfig                          # whitespace + line-ending standardization
```

---

## Data flow

### Add an account
```
User → AddAccountDialog
  → AddAccountViewModel (IMAP/SMTP form)
  → ImapService.TestConnectionAsync (verify before save)
  → JsonAccountStore.Save (config to %LOCALAPPDATA%\GreatEmailApp\accounts.json)
  → WindowsCredentialStore.Save (password to Windows Credential Manager
       under "GreatEmailApp:{accountId}", LOCAL_MACHINE persistence)
  → MainViewModel.OnAccountAdded → LoadFoldersAsync → ImapService.ListFoldersAsync
```

### Open a folder
```
Sidebar click
  → MainViewModel.SelectFolderAsync (RelayCommand)
  → ImapService.ListMessagesAsync (200 most recent, §14 limit)
  → ObservableCollection<MessageViewModel> updates → MailList re-renders
```

### Open a message
```
MailList row click
  → MainViewModel.SelectMessageAsync
  → ImapService.FetchBodyAsync → ReadingPane re-binds via OnBodyLoaded()
  → DispatcherTimer armed for Settings.MarkReadDelaySeconds
  → On tick (still selected): ImapService.SetSeenAsync(seen=true)
       → optimistic UI: row goes from bold→normal, sidebar unread count -1
```

### Theme change
```
SettingsDialog (Appearance tab)
  → SettingsViewModel.Theme/Accent setter
  → App.Theme.Apply(theme, accent)
       → swap Themes/Light.xaml ↔ Themes/Dark.xaml in MergedDictionaries
       → override AccentBrush/Hover/Pressed/Soft via Application.Current.Resources
  → All `{DynamicResource ...}` consumers re-evaluate automatically
```

---

## Security boundaries (per rulebook §5)

| Boundary | Rule |
|---|---|
| IMAP/SMTP passwords | Windows Credential Manager only. Never JSON, never plaintext, never Firestore. |
| Firebase auth tokens (Phase 4) | DPAPI-encrypted `auth.dat` in `%LOCALAPPDATA%`. Per-user. |
| Settings synced to Firestore | Account configs (host/port/encryption), UI prefs, rules. **Passwords excluded.** |
| TLS | All IMAP/SMTP via SSL/TLS or STARTTLS. Plaintext refused unless explicitly enabled. |
| HTML email remote images | Disabled by default; per-sender trust list (Phase 4). |

---

## Persistence locations

```
%LOCALAPPDATA%\GreatEmailApp\
├── accounts.json        # account configs (no passwords)
├── settings.json        # AppSettings
├── cache.db             # SQLite message cache (Phase 5)
└── logs/                # local logs

Windows Credential Manager
└── GreatEmailApp:{accountId}   # IMAP/SMTP password per account

Firestore (Phase 4, when sync enabled)
└── users/{uid}/settings   # synced subset of settings — never passwords
```

---

## How to run

```powershell
cd "E:\The Great Email App\src"
dotnet build
dotnet run --project GreatEmailApp
```

Or use the `relaunch` skill (kills running instance, rebuilds, relaunches).

---

## Where to look when…

| Task | Files |
|---|---|
| Add a new theme color | `Themes/Light.xaml` + `Themes/Dark.xaml` (matching key in both) |
| Add a new ribbon button | `Controls/Ribbon.xaml` (the relevant tab block) + bind to a `[RelayCommand]` on `MainViewModel` |
| Add a new IMAP operation | `IImapService.cs` + `ImapService.cs` (return `Result<T>`); call from `MainViewModel` |
| Add a setting | `AppSettings.cs` (model) + `SettingsViewModel.cs` (binding) + `SettingsDialog.xaml` (UI row) |
| Add a context menu item | The relevant control's XAML `<ContextMenu>` + a code-behind `_Click` handler that resolves target via `ContextMenu.PlacementTarget.Tag` |
| Add a new account-level operation | `MainViewModel` command + ribbon/sidebar wiring |

---

## Phase status

See `Project/roadmap.md`. As of writing: Phase 1 (shell), Phase 2 (IMAP), Phase 3 (settings) all shipped. Phase 4 (Firebase sync) deferred. Currently between phases — message actions, context menus, and AI roadmap (P3-AI-1..5) just landed.
