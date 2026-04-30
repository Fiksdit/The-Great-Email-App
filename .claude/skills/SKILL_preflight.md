---
name: preflight
description: "TRIGGER THIS SKILL before starting ANY task on The Great Email App. Call it when: the user says 'preflight', 'check yourself', 'read the instructions', 'follow the rulebook', 'start fresh', or at the beginning of any new session. This skill enforces the AI Session Start Checklist from the rulebook and prevents Claude from guessing, assuming, or working on the wrong files."
---

# Pre-Flight Checklist â€” The Great Email App

## Purpose

This skill exists because AI sessions start blank. Without this checklist the AI will skip reading files before editing them, assume things that aren't true, and make multiple failed attempts instead of verifying fundamentals first. This is the hard stop that prevents that.

**Complete every step below before writing ANY code or file output.**

---

## Step 0: Session Log

Before anything else, make sure today's session log exists.

1. Check for `Project/sessions/session-YYYY-MM-DD.md` (today's date â€” replace with actual date).
2. If missing, create it with this header:
   ```md
   # Session Log â€” YYYY-MM-DD
   Raw shorthand notes. Local only (gitignored). EOD summary lives in session-summary-YYYY-MM-DD.md.
   ```
3. Append one line after every meaningful unit of work throughout the session:
   - Fix applied â†’ `HH:MM  FIX-YYYY-MM-DD-NNN  <one-line summary>`
   - Finding â†’ `HH:MM  INVESTIGATE  <what you found>`
   - Blocker â†’ `HH:MM  BLOCKER  <what's blocking>`
   - Commit â†’ `HH:MM  COMMIT  <sha>  <subject>`
   - Decision â†’ `HH:MM  DECISION  <what was decided>`
4. The EOD skill consumes this file. If it doesn't exist, the end-of-day summary will be missing half the work.
5. **Bug fixes go in two places.** The session log gets the one-line `FIX-YYYY-MM-DD-NNN` shorthand. The structured entry â€” symptom, what was tried, what worked, files moved â€” goes in `Project/logs/fix_log.md` via `/log-fix` (see rulebook Â§16 Fix Log Protocol). The session log is short-term memory; the fix log is the permanent audit trail.

**Output:** "Session log: Project/sessions/session-YYYY-MM-DD.md â€” [created / exists, will append]."

---

## Step 1: Git Sync

1. Run `git status` â€” are there uncommitted files from a previous session?
2. If yes: commit stragglers with a `chore:` message before starting new work. Never leave files between sessions.
3. Run `git pull origin main` â€” ensure local is up to date.

**Output:** "Git: [clean / N straggler files committed]. Up to date with origin."

---

## Step 2: Read the Rulebook

Read `Project/rulebook.md`. Focus on sections relevant to the current task:
- **Â§1** â€” AI Session Start Checklist (follow literally)
- **Â§2** â€” Surgical Change Rule (if it works, don't touch it)
- **Â§8** â€” Known Issues (is this already diagnosed?)
- **Â§9** â€” CRUD Standards (return shapes, validation, limits)
- **Â§14** â€” Query & Data Limits (every list query needs an explicit limit)
- Any section specifically relevant to today's task

**Output:** "Rulebook read. Relevant sections: [list them]."

---

## Step 3: Check Known Issues + Fix Log

Two places to check before assuming this task is fresh ground:

1. **Rulebook Â§8 Known Issues table** â€” is the current task already listed there?
2. **`Project/logs/fix_log.md`** â€” search for any prior `FIX-YYYY-MM-DD-NNN` entry on the same file, area, or symptom. If one exists:
   - Read its **Tried** field â€” **do not repeat a failed approach**
   - Read its **Fix** field â€” the bug may already be solved and your job is to verify it didn't regress
   - Read its **Files changed** + **Rulebook** fields for context

This is the single highest-leverage step in the whole preflight. The fix log exists specifically so future sessions don't re-run debugging paths that already failed. Skipping it means burning hours rediscovering things that are already documented.

**Output:** "Known Issues: [relevant Â§ entry / none]. Fix log: [matching FIX-NNN if found / no prior entries on this area]."

---

## Step 3.5: Protected File Check â›”

Before identifying files, check whether the task involves ANY protected file. **This is a hard stop.**

Protected files are any file with a `ðŸ”’ LAYOUT` or `ðŸ”’ PROTECTED` banner in its header, plus any file listed in the rulebook's Layout File Protection section.

**If the task modifies any protected file:**

1. **STOP.** Do not proceed.
2. State: "â›” This task touches a protected file: [path]. Explicit owner approval required."
3. Describe exactly what the change would be and why.
4. **Wait for James Reed to approve before proceeding.**

**Output:** "Protected file check: [no protected files involved / â›” BLOCKED â€” [path] is protected, awaiting owner approval]."

---

## Step 4: Identify the Files

Before touching ANY file:

1. **What file am I modifying?** State the exact path.
2. **Is that file actually used where I think it is?** Grep to verify it's imported:
   ```bash
   grep -r "ComponentName\|functionName" src/ --include="*.ts" --include="*.tsx"
   ```
3. **What is the current rev?** Read the file header.
4. **Does this file have tests?** If yes, note them â€” changes must not break them.

**Output:** "File: [path] Â· Rev [N] Â· Imported by [file(s)] Â· Tests: [yes/no/none]"

---

## Step 5: Verify the Database (if DB-touching code)

If the task involves any database read or write:

1. Check the actual schema â€” column names, types, constraints, NOT NULL, defaults
2. Check existing query patterns in the codebase before writing new ones
3. Never invent business rules, filters, or exclusions the owner didn't request

**Output:** "Schema verified for [table]. Key columns: [list]. Constraints: [list]."

If not DB-touching: "No DB changes â€” skipping schema verification."

---

## Step 6: State the Plan

Before writing a single line of code:

1. **What exactly am I changing?** List specific files and specific changes.
2. **What am I NOT changing?** Explicitly state what stays untouched.
3. **What format?** Full file output or CCCP?
4. **What's the risk?** Could this break anything else?

**Output:** The plan, stated clearly, waiting for user approval before proceeding.

---

## Step 7: Verify After First Attempt

If a fix doesn't work on the first try:

1. **STOP.** Do not try a second variation of the same approach.
2. Re-verify: is the component actually rendered on the affected page?
3. Re-verify: is the file you edited actually imported where you think?
4. State what you found before proposing anything new.
5. After 3 failed attempts: stop and ask the user to provide the file directly.

**Never do more than 2 attempts without re-verifying fundamentals.**

---

## Quick Reference: Code Standards

- Read the file before modifying it â€” always
- File header: Created / Revised / Rev / Changed-by on every AI-touched file
- Correct auth pattern: `IMAP/SMTP via MailKit (passwords in Windows Credential Manager); Firebase Auth via Google sign-in for settings sync (Phase 4)`
- Return shape: `{ success: true, data } | { success: false, error }`
- All list queries: explicit `.limit()` â€” see Â§14
- No `useEffect` for data fetching (if using React)
- No placeholders, stubs, or TODOs
- TypeScript: no `as any` â€” use proper types

---

## Exact Replication Protocol

When the user says "do the same thing on [file B]" or "apply this fix to [other files]":

1. **Re-read the file you just changed** â€” get the exact diff (before vs after).
2. **Read the target file** â€” find the corresponding section.
3. **Apply the identical pattern** â€” same structure, same naming convention, same approach. Do not "improve" or "adapt" it.
4. **Diff-check yourself** â€” verify the structural change is identical between files (adjusted only for inherently different names).

**Why:** AI repeatedly applies the same fix differently across sibling files in the same session. This creates inconsistency and wastes time re-doing the work.

---

## The Golden Rule

**If you don't have the file, don't guess.** Read it. If you can't find it, use grep or glob. You have the tools â€” use them. Never assume a file's contents, a component's location, or a database column's name.
