---
name: log-fix
description: "TRIGGER THIS SKILL after every diagnosed-and-fixed bug on The Great Email App. Call it when: the user says 'log this fix', 'log-fix', 'add to fix log', 'document this bug', 'this is fixed', or right after any bug fix lands. Per rulebook §16, every bug-class change gets a structured entry in Project/logs/fix_log.md so future sessions don't re-run failed debugging paths. The 'Tried' field is mandatory — even 'nothing — fixed on first attempt' is a valid value."
---

# Fix Log Entry — The Great Email App

Append a new structured entry to `Project/logs/fix_log.md` documenting the bug, the root cause, what was tried, and what worked.

## When to use

Add an entry when you:
- Resolve a reported bug
- Patch a silent failure (data not loading, wrong totals, missing records)
- Work around a third-party limitation
- Fix a regression — even a small one
- Discover a root cause that took more than one attempt to find

Do **not** add entries for new features, cosmetic-only tweaks, or behaviour-preserving refactors. Those go to the session log + commit message.

## Procedure

1. **Read** the most recent entry in `Project/logs/fix_log.md` to determine the next sequence number for today (`FIX-YYYY-MM-DD-NNN`, NNN = zero-padded daily counter starting at 001).
2. **Append** the entry below at the **top** of the entries section (newest first), separated by `---`.
3. **Commit** the change immediately. The fix log is committed to git per rulebook §16; never `.gitignore` it.

## Template

```markdown
---

## FIX-YYYY-MM-DD-NNN

**Area:** [module or screen affected, e.g. "Sidebar / Folder tree", "Add Account dialog", "ImapService", "Theme system"]
**Status:** ✅ Fixed / ⚠️ Open / ❌ Won't Fix / 🔁 Regressed
**Priority:** P1 / P2 / P3

**Symptom:** [What the user sees, in their words.]

**Replicate:**
1. [Step]
2. [Step]

**Root cause:** [One sentence: what in the code caused this.]

**Tried:**
- [Approach that didn't work, and why — this field is mandatory and is the whole point of the log]

**Fix:** [What was changed. File · old → new.]

**Files changed:**
- `path/to/file.ext` (Rev N → N+1)

**Rulebook:** [§N if a rulebook section covers this pattern. "None" if not.]

**Session:** YYYY-MM-DD
**Commit:** `xxxxxxx`
```

## Rules

1. **NNN is per-day** — `001`, `002`, `003`, resets each calendar day.
2. **Newest entries on top** so a future session sees the latest first when scanning.
3. **The "Tried" field is mandatory.** Future sessions read this to avoid re-running failed experiments. "Nothing — fixed on first attempt" is valid; blank is not.
4. **Append-only.** Never delete entries. If a fix regresses, mark the original `🔁 Regressed` and add a new entry that points back to it.
5. **Commit immediately** after appending, with a message like `Fix log: FIX-2026-04-30-001 — short description`.

## Output format

When run, the skill should:
1. Confirm the fix details with the user (Area, Symptom, Root cause, Fix).
2. Pre-fill the template with what's known from the conversation.
3. Ask only for the field that's actually missing — usually "Tried" since that lives in the AI's memory of failed attempts.
4. Write the entry to the file and stage it.
5. Show the user the entry before committing so they can sanity-check.
