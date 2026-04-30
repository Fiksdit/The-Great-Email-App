# Release Process — The Great Email App

How a new build gets from a commit on `main` to existing installs picking it up via the in-app updater. Read this before bumping the version.

## Distribution architecture

- **Releases** live on GitHub: `https://github.com/Fiksdit/The-Great-Email-App/releases`. Repo is **public** (must stay public — the in-app updater hits the unauthenticated REST API; private repos return 404 and the updater treats that as "no releases").
- **Asset name** is required to be `GreatEmailApp-v{X.Y.Z}.zip`. The updater (`GitHubUpdateService`) finds the asset by prefix `GreatEmailApp-` + suffix `.zip`. If you name it differently the user sees "Release vX.Y.Z has no matching asset".
- **Tag name** must be `v{X.Y.Z}` (with the `v`). The parser in `GitHubUpdateService.TryParseTag` accepts both `v1.2.3` and `1.2.3`, but tagging `v…` keeps Git happy and matches every prior release.
- **Versioning**: bump the `<Version>` (and matching `<AssemblyVersion>` / `<FileVersion>`) in `src/GreatEmailApp/GreatEmailApp.csproj`. The publish script can do this for you with `-Version 0.x.y`.
- **Build artifact** lives at `dist/GreatEmailApp-v{X.Y.Z}.zip` after running `scripts/publish.ps1`. The `dist/` folder is gitignored.

## Tools required on the dev box

- **gh CLI** (`winget install GitHub.cli`) — used to create the release and upload the asset in one shot. Must be authenticated once: `gh auth login` → GitHub.com → HTTPS → web browser. Login persists across sessions.
- .NET 8 SDK (already installed per preflight Step 00).
- PowerShell 7+ (`pwsh`) — `publish.ps1` uses pwsh-only syntax in places.

## Standard release flow

For a routine bump (e.g. 0.4.1 → 0.4.2):

```powershell
# 1. Bump csproj + build framework-dependent win-x64 → dist/GreatEmailApp-v0.4.2.zip
pwsh scripts/publish.ps1 -Version 0.4.2

# 2. Commit the version bump and push
git add -A
git commit -m "Bump to 0.4.2"
git push origin main

# 3. Tag and push the tag
git tag v0.4.2
git push origin v0.4.2

# 4. Create the GitHub release with the zip attached
gh release create v0.4.2 dist/GreatEmailApp-v0.4.2.zip `
  --title "v0.4.2 — <one-line summary>" `
  --notes "<markdown release notes>"
```

That last `gh release create` is what existing installs detect on their next "Check for updates" click.

Within ~30s of the release going live, anyone running an older 0.x.x build can update via **Settings → About → Check for updates → Download & install**.

## Release notes — what to include

Users see this in two places: the GitHub Release page and the "What's new in vX.Y.Z" panel inside the in-app updater (which renders the body of the release as plain text). Keep it short. Suggested template:

```markdown
<one-paragraph summary of why this matters>

## What's new
- <bullet 1>
- <bullet 2>

## Breaking changes / migration  (only if any)
- <thing user must know>

## How to update
Settings → About → **Check for updates** → **Download & install**.
```

## Checklist before tagging

- [ ] csproj `<Version>` matches the tag.
- [ ] `dotnet build -c Release` is clean (publish.ps1 already does this; abort the release if it errors).
- [ ] Smoke-launched the resulting exe out of `bin/Release/net8.0-windows/win-x64/publish/` to confirm it doesn't immediately crash.
- [ ] Release notes mention any change to: AppSettings shape, sync schema, OAuth client config, supported .NET runtime — anything that affects users mid-upgrade.
- [ ] If you changed sync behavior, re-read `Project/logs/fix_log.md` FIX-2026-04-30-002 first. Do not regress it.

## When NOT to bump

Don't tag a release for: comment-only changes, dev-only scripts, .gitignore tweaks, fix-log additions. Wait until the next user-visible change rides along.

## What "framework-dependent" means here

`publish.ps1` does `--self-contained false`. The zip has just our exe + Core dll + transitive package dlls (~4 MB). Users need the .NET 8 Desktop Runtime installed on their box; we don't ship the framework. This is the right tradeoff because:

- Most target users already have .NET 8 (it's bundled with various Microsoft installs).
- Self-contained pumps the zip up to ~80 MB and adds no real value.
- The runtime can be installed by the user once with `winget install Microsoft.DotNet.DesktopRuntime.8`.

If we ever need to ship to users who can't install runtimes (kiosk machines, locked-down environments), flip `--self-contained true` and add `-r win-x64 -p:PublishSingleFile=true` — but that's a future call.

## What lives where on a user machine after install

| Path | What | Roams? |
|------|------|--------|
| `<install dir>\GreatEmailApp.exe` | binary | n/a |
| `<install dir>\appsettings.json` | Firebase + OAuth IDs (public) | n/a |
| `%LOCALAPPDATA%\GreatEmailApp\accounts.json` | account configs (synced) | per-PC, but cloud-restored |
| `%LOCALAPPDATA%\GreatEmailApp\settings.json` | preferences (synced) | per-PC, but cloud-restored |
| `%LOCALAPPDATA%\GreatEmailApp\auth.dat` | DPAPI-encrypted Firebase refresh token | per-PC + per-user, never roams |
| `%LOCALAPPDATA%\GreatEmailApp\sync-meta.json` | local sync bookkeeping | per-PC, never synced — this is intentional, see FIX-2026-04-30-002 |
| `%LOCALAPPDATA%\GreatEmailApp\WebView2Data\` | Edge runtime user-data | per-PC |
| Windows Credential Manager (`GreatEmailApp\<accountId>`) | IMAP/SMTP passwords | per-PC + per-user, never roams |

The updater's swap script (`apply-update.cmd`) preserves all of `%LOCALAPPDATA%\GreatEmailApp\` because it only mirrors files into the install dir — user data lives outside the install dir, untouched.
