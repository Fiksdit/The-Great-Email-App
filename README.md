# The Great Email App

A focused, native-Windows IMAP email client with Outlook's familiar ribbon UX, dark/light theming, and Firebase-backed settings sync.

**Stack:** WPF + .NET 8 (C#) · MailKit · SQLite · Firebase Auth + Firestore
**Owner:** James Reed · coolman0804@outlook.com
**Repo:** https://github.com/Fiksdit/The-Great-Email-App

---

## Status

🔧 **Phase 1 — Visual shell** (in progress / 2026-04-29)

| Phase | Goal | Status |
|-------|------|--------|
| 1 | WPF shell, themes, three-pane layout, ribbon | 🔧 |
| 2 | IMAP via MailKit, Add Account, password storage in WCM | 📋 |
| 3 | Settings dialog & local persistence, File backstage | 📋 |
| 4 | Google sign-in, Firestore settings sync | 📋 |
| 5 | Compose, search, notifications, HTML email | 📋 |

See [`Project/roadmap.md`](Project/roadmap.md) for the full P0–P3 plan.

---

## Repo layout

```
src/                       # WPF + Core
  GreatEmailApp/           # WPF app (UI)
  GreatEmailApp.Core/      # Models, services, sample data (no WPF deps)
  GreatEmailApp.sln
docs/design/               # Original Claude Design HTML/JSX mockup (reference)
Project/
  rulebook.md              # Mandatory development rules
  roadmap.md               # P0–P3 roadmap & decision log
  sessions/                # Daily session logs (gitignored)
  logs/fix_log.md          # Persistent fix log (committed)
.claude/skills/            # In-project AI skills (preflight, eod, etc.)
project-guidelines/        # Original templates (reference)
```

---

## Build & run

**Prereqs:** Windows 10/11, .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8`).

```bash
cd src
dotnet build
dotnet run --project GreatEmailApp
```

For development, Visual Studio 2022 with the ".NET desktop development" workload gives you the XAML designer + debugger.

---

## Architecture notes

- **MVVM** via `CommunityToolkit.Mvvm` (`ObservableObject`, `RelayCommand`, source-gen properties)
- **Theme system:** `ResourceDictionary` swap for light/dark + runtime accent override (`Services/ThemeManager.cs`)
- **Icons:** Segoe Fluent Icons font (built into Windows 11) — glyph codepoints in `Themes/Icons.xaml`
- **Three-pane layout:** WPF `GridSplitter` with min/max widths from `Themes/Tokens.xaml`
- **Sample data:** mirrors the design mockup so visuals match before IMAP wiring lands

---

## Contributing

Read [`Project/rulebook.md`](Project/rulebook.md) before any work — it covers file headers, surgical change rule, query limits, error handling, and security boundaries (passwords stay in Windows Credential Manager; only non-secret settings sync to Firestore).
