---
name: sync
description: Full sync cycle for The Great Email App — pull latest from GitHub, merge intelligently across PCs, run the .NET build gate, then push. Use when the user says 'sync', 'pull from github', 'pull latest', 'sync changes', or 'merge from main'.
---

# Sync — Pull + Push (The Great Email App)

## Multi-PC reality (read this first)

James develops across at least two PCs:

- **Dev PC** — primary development, where releases are cut.
- **This PC** (`E:\Apps\The-Great-Email-App`) — runs the **installed release** at `%LOCALAPPDATA%\Programs\GreatEmailApp\`; the source tree here gets used for quick fixes and verification.

Both PCs push to the same `main` on GitHub. There is no other engineer — every conflict is James-vs-James-at-different-times. The file-header `Revised:` date is the most reliable hint at "which version is the newer authoring intent." When in doubt, prefer the side with the higher `Rev:` number.

**Until a release is cut and the in-app updater runs**, code changes on either PC do not affect the running installed app. Mention this in the Step 9 summary so the operator knows whether they still need to ship a release.

---

## Local-only state — NEVER commits, NEVER pulls

The repo's `.gitignore` should keep these out. If any show up in `git status` as staged or tracked, unstage before pulling — they're per-machine state, not source.

| Path | What | Why local-only |
|---|---|---|
| `%LOCALAPPDATA%\GreatEmailApp\sync-meta.json` | Last-pushed timestamp | Intentionally not synced (FIX-2026-04-30-002). |
| `%LOCALAPPDATA%\GreatEmailApp\accounts.json` | Account roster | Per-machine state; SyncCoordinator handles the cloud copy. |
| `%LOCALAPPDATA%\GreatEmailApp\auth.dat` | DPAPI-encrypted Firebase token | DPAPI-keyed to Windows user. |
| `%LOCALAPPDATA%\GreatEmailApp\cache.db*` | SQLite message cache | Rebuilds from IMAP. |
| `%LOCALAPPDATA%\GreatEmailApp\vault.dat` | Unwrapped data key cache | DPAPI-keyed; per-machine. |
| `%LOCALAPPDATA%\GreatEmailApp\WebView2Data\` | WebView2 user data | Per-machine browser state. |
| `Project/sessions/session-*.md` | Daily session logs | Gitignored per rulebook §15. |

All of these live OUTSIDE the repo, so they shouldn't appear in `git status` at all. If they do, something is wrong — investigate before continuing.

---

## What DOES roundtrip and needs careful merging

| Path | Hazard |
|---|---|
| `Project/rulebook.md` | Two PCs may add a §N section independently → renumber, don't overwrite. |
| `Project/logs/fix_log.md` | Daily sequences `FIX-YYYY-MM-DD-NNN` collide when both PCs ship a fix on the same day. Renumber, never drop entries. |
| `Project/roadmap.md` | Item check-offs / new items can land on both sides. Union them. |
| `src/**/*.cs`, `src/**/*.xaml` | The actual code — see Step 4 rules. |
| `.claude/commands/*.md` | Skill bodies. Same merge logic as rulebook. |
| `appsettings.json` | Build-time config (public Firebase keys, OAuth client ID). Never local. |

---

## Step 1: Orient

Run these in parallel and report back:

```bash
git branch -a
git status
git stash list
git log --oneline -10
git remote -v
```

State: current branch, target branch (default `main`), uncommitted/stashed work, last 10 commits, remote URL. If the remote points anywhere other than the user's GitHub repo, STOP and confirm.

---

## Step 2: Stash local changes (if dirty)

```bash
git stash push -u -m "auto-stash before sync — {branch} {timestamp}"
```

`-u` includes untracked files so a newly-added file isn't lost. If `git status` is already clean, skip.

---

## Step 3: Fetch and assess divergence

```bash
git fetch origin
git log --oneline HEAD..origin/main     # incoming
git log --oneline origin/main..HEAD     # outgoing
git diff --stat HEAD...origin/main      # overlap surface
```

| Situation | Strategy |
|---|---|
| Local has no commits ahead (fast-forward) | `git pull --ff-only origin main` — done. |
| Diverged, no overlapping files | `git pull --no-rebase origin main` — auto-merge, no conflict risk. |
| Diverged, overlapping files | Full 3-way analysis (Step 4) before merging. |

Do not use rebase as the default. This is a solo project across PCs — preserving the merge commit makes the cross-PC history easier to read.

---

## Step 4: 3-way conflict analysis (when files overlap)

For every file touched on both sides, gather all three versions before deciding:

```bash
# Common ancestor
git show $(git merge-base HEAD origin/main):src/path/to/File.cs

# Our (local) version
cat src/path/to/File.cs

# Their (remote) version
git show origin/main:src/path/to/File.cs
```

Answer for each:
1. What did THIS PC change vs ancestor?
2. What did the OTHER PC change vs ancestor?
3. Are the changes in the same lines (true conflict) or different sections (auto-merge safe)?

Then start the merge: `git pull --no-rebase origin main` and resolve any conflicts using the rules below.

### Conflict resolution rules — bias toward auto-merging safely

You are the merger. Resolve confidently using the table. Only `Ask` when the table genuinely doesn't decide it.

| Scenario | Rule |
|---|---|
| Security tightening (auth guard, validation, DPAPI, TLS) | **Take the tighter side** — always. |
| Bug fix referenced by a `FIX-YYYY-MM-DD-NNN` entry in `fix_log.md` | **Take it in** — note the FIX id in summary. |
| Rulebook violation in incoming (e.g. missing file header, swallowed exception) | Reject the violating change — keep compliant local. Flag in summary. |
| File header — different `Revised:` dates | Keep the LATER date. |
| File header — different `Rev:` numbers | Keep the HIGHER rev. |
| `using` directives | Union both sides (keep all imports). |
| New type members / record fields added on both sides | Union — keep ALL fields from both sides. |
| Public method signatures changed on both sides | Merge behavior. If intent unclear, **Ask**. |
| Auto-mark-read / sync / poller timing constants | Keep the value from the side whose commit message references tuning. If neither, keep local. |
| Tray notifier / unhandled exception hardening (FIX-2026-05-12-002) | Never weaken — toast failures must never crash. |
| SyncCoordinator.ShouldPreferLocalOver / sync-meta.json guards (FIX-2026-04-30-002) | **Load-bearing** — never weaken or remove. Reject any merge that does. |
| Sample data fallback re-introduced anywhere | Reject — removed for cause in FIX-2026-04-30-002. |
| `RenderOptions.ProcessRenderMode = SoftwareOnly` in App.OnStartup | Keep — load-bearing per FIX-2026-04-30-001. |
| New rulebook §N section on both sides | Renumber the lower-priority one to the next available N. Update the TOC. |
| Duplicate `FIX-YYYY-MM-DD-NNN` headers in fix log | Renumber the second one to `-NNN+1`. Both entries stay. |
| `roadmap.md` checklist edits | Union — if both check off the same item, keep checked. If one adds an item, keep it. |
| Unsure | **Ask** — never silently discard. |

After resolving, stage and commit the merge with a message that lists each conflicting file and the rule applied. Example:

```
Merge origin/main — resolve conflicts

- src/.../MainViewModel.cs: union new field + keep local body-render fix (FIX-2026-05-12-001)
- Project/logs/fix_log.md: renumbered incoming FIX-2026-05-12-003 → FIX-2026-05-12-004 (collision)
- Project/rulebook.md: renumbered incoming §18 → §19 (collision with local §18)
```

---

## Step 5: Restore stashed changes

```bash
git stash pop
```

Conflicts on pop use the same Step 4 rules. The stashed changes are always "ours" (the work you just did before the sync).

If pop fails because the index isn't clean, finish committing the merge first, then pop.

---

## Step 6: Cross-PC safety checks

Run after merge resolution, before the build gate:

```bash
# 1. SyncCoordinator guards intact (FIX-2026-04-30-002)
grep -n "ShouldPreferLocalOver" src/GreatEmailApp.Core/Sync/SyncCoordinator.cs
grep -n "sync-meta" src/GreatEmailApp.Core/

# 2. Software render mode intact (FIX-2026-04-30-001)
grep -n "ProcessRenderMode" src/GreatEmailApp/App.xaml.cs

# 3. Fix log ID collisions
grep "^## FIX-" Project/logs/fix_log.md | sort | uniq -d

# 4. Rulebook section numbering — no duplicate §N
grep -E "^## [0-9]+\." Project/rulebook.md | sort -t. -k1.4n | uniq -d -w 6

# 5. No accidentally-committed local state
git diff --cached --name-only | grep -E "(sync-meta|accounts\.json|auth\.dat|cache\.db|vault\.dat|WebView2Data|crash\.log)"
```

Any hit on the local-state grep is a HARD STOP — unstage immediately, those files don't belong in the repo. Any hit on duplicate fix log IDs or section numbers needs to have been resolved during Step 4; if it shows up here, re-do that piece.

---

## Step 7: Build gate ⛔ HARD STOP

**Do NOT push until this passes. No exceptions.**

```bash
"C:/Program Files/dotnet/dotnet.exe" build "E:/Apps/The-Great-Email-App/src/GreatEmailApp.sln" -c Debug -nologo
```

| Result | Action |
|---|---|
| `0 Error(s)` | ✅ proceed |
| Any error | 🛑 do NOT push. List errors and ask: "Fix and re-sync, or hold?" |
| Warning count > previous known baseline (~70) | Note in summary, don't block. |

If the EXE is locked by a running dev instance:

```powershell
Get-Process -Name GreatEmailApp -ErrorAction SilentlyContinue | `
  Where-Object { $_.Path -like "*\bin\Debug\*" } | Stop-Process -Force
```

That only kills the Debug-path instance; the installed copy at `%LOCALAPPDATA%\Programs\GreatEmailApp\` is unaffected.

---

## Step 8: Push

```bash
git push origin main
```

| Result | Action |
|---|---|
| Push succeeds | ✅ done with the git side. |
| Rejected — remote moved again | Loop back to Step 3 and remerge. |
| Hook failure (pre-push) | Investigate the failure. Never bypass with `--no-verify`. |

---

## Step 9: Summary report

Report back to the operator:

- **Pulled commits** — short list of subjects.
- **Files with conflicts** — each one and the rule applied (cite the table row).
- **Renumbering** — any FIX-NNN or §N that moved, with the old → new id.
- **Safety check results** — SyncCoordinator guards intact, software render mode intact, no committed local state.
- **Build status** — `N error(s), M warning(s)`.
- **Push** — confirmed pushed (or held with reason).
- **Reminder for delivery to the installed app on this PC:**
  > To get these changes into the installed copy at `%LOCALAPPDATA%\Programs\GreatEmailApp\`, build a release on the dev PC, push the tag, then on this PC: Settings → About → Check for updates.
- **Reminder for the dev PC:** if today's session here is the source of new commits, the dev PC needs `git pull` next time it's used.

---

## When NOT to use this skill

- Mid-merge of an in-progress conflict from a previous attempt — finish that one first, don't restart the cycle on top.
- During an active release-build window — let the release tag and release zip settle on GitHub first.
- If `git remote -v` shows an unexpected URL — confirm with the user before pulling from a fork or wrong remote.
