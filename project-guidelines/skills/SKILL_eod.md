---
name: eod
description: "TRIGGER THIS SKILL at the end of any {{PROJECT_NAME}} work session. Call it when: the user says 'EOD', 'end of day', 'wrap up', 'session summary', 'what did we do today', or 'save progress'. Generates a structured session summary, updates the rulebook and roadmap with anything new discovered, and prepares the handoff for the next session."
---

# End of Day — Session Summary Generator

## Purpose

Generate a clean session summary at the end of every work session. This file becomes the handoff document between this session and the next. Chat memory is wiped between sessions — this file is not.

The summary captures:
1. What was changed (files, DB, config)
2. What was discovered (bugs found, decisions made, patterns learned)
3. What was resolved vs what remains open
4. What the next session must do first

---

## Step 1: Review the Session Log

Read `Project/sessions/session-YYYY-MM-DD.md` for today's session.

If the log is empty or missing — reconstruct from conversation history:
- Every file created or modified (path + rev)
- Every database change (SQL run, migrations applied)
- Every bug found and fixed
- Every bug found and NOT fixed
- Every feature built
- Every decision made
- Every rulebook violation encountered

---

## Step 2: Flush Outstanding Log Entries

Before generating the summary, make sure anything that happened after the last log append is captured:

- Last fix or change made → `HH:MM  FIX/COMMIT/DECISION  <summary>`
- Any open blocker → `HH:MM  BLOCKER  <description>`

Append these to the session log first, then proceed.

---

## Step 3: Update the Rulebook

If any new patterns, lessons, or rules emerged this session:

1. Open `Project/rulebook.md`
2. Add to §16 Lessons Learned (or the appropriate section)
3. If a Known Issue was resolved → update §8 status to `✅ Fixed YYYY-MM-DD`
4. If a new Known Issue was discovered → add it to §8
5. Bump the rulebook revision number and update the date in the header

Do not add verbose prose — keep rulebook entries concise and actionable.

---

## Step 4: Update the Roadmap

If any roadmap items changed status this session:

1. Open `roadmap.md`
2. Mark completed items `✅ DONE YYYY-MM-DD`
3. Mark newly started items `🔧 IN PROGRESS`
4. Add any new items discovered this session
5. Move completed items to the Shipped Log section
6. Update the roadmap date in the header

---

## Step 5: Generate the Session Summary File

Create `Project/sessions/session-summary-YYYY-MM-DD.md` with these sections:

```markdown
# Session Summary — YYYY-MM-DD
**Project:** {{PROJECT_NAME}}
**Duration:** ~X hours
**Engineer:** {{OWNER_NAME}} + {{AI_MODEL}}

---

## What Was Done

| # | Item | Files Changed | Status |
|---|------|--------------|--------|
| 1 | (description) | path/to/file.ts (Rev N→N+1) | ✅ Done |
| 2 | (description) | — | ✅ Done |

---

## Commits Pushed

| SHA | Message |
|-----|---------|
| xxxxxxx | commit subject |

---

## Bugs Fixed

| Fix ID | Description | Root Cause |
|--------|-------------|-----------|
| FIX-YYYY-MM-DD-001 | (description) | (root cause) |

---

## Bugs Discovered (Open)

| # | Description | Priority | Next Step |
|---|-------------|---------|-----------|
| 1 | (description) | P1/P2/P3 | (what to do) |

---

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| (decision) | (why) |

---

## Rulebook Changes

- §X updated: (what changed)
- §8 Known Issues: (what was added/resolved)

---

## Roadmap Changes

- (item ID) marked ✅ DONE
- (new item) added as (P1/P2/P3)

---

## Next Session Must Do First

1. (highest priority item for next session)
2. (second priority)
3. (third priority)

---

## Blockers

| Blocker | Waiting On |
|---------|-----------|
| (description) | (person / external / decision) |
```

---

## Step 6: Commit Everything

```bash
git add Project/sessions/session-summary-YYYY-MM-DD.md Project/rulebook.md Project/roadmap.md Project/logs/fix_log.md
git commit -m "docs(eod): session summary YYYY-MM-DD — [one-line summary of main work]"
git push origin main
```

The session log itself (`session-YYYY-MM-DD.md`) is gitignored — do not commit it.

---

## Step 7: Output to User

Tell the user:
1. What the summary file is named and where it was saved
2. The 3 most important things that happened this session (bullet points)
3. The top priority for the next session
4. Any blockers that need owner action before next session

Keep it short — the file has the detail. The verbal summary is the headline.
