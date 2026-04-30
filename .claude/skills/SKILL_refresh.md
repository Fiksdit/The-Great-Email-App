---
name: refresh
description: "TRIGGER THIS SKILL whenever the AI's context has been compacted, summarized, or reset mid-session on The Great Email App. Call it when the user says 'refresh', 'reread', 're-read', 'reload', 'rehydrate', 'recompact', 'post-compact', 'compaction recovery', 'context reset', 'restart context', 'you got compacted', 'where were we', 'get back on track', 'catch up', or 'reorient'. This skill forces the AI to re-read the rulebook, fix log, today's session log, and any active files mentioned in the compaction summary so it doesn't drift after losing chat memory."
---

# Refresh â€” Post-Compaction Recovery

## Why This Exists

Context compaction silently drops everything in the chat that wasn't in the summary. The AI keeps talking like nothing happened, but its grounding is gone:

- It forgets the file revs it just edited
- It forgets the rulebook sections it had loaded
- It forgets the FIX entry it was about to write
- It forgets which files have been touched in this session and which are still pristine
- It "remembers" snippets of files that may already be out of date

The summary is **lossy by design** â€” it's a compressed paraphrase, not a snapshot. Treating it as ground truth is how the AI starts editing the wrong rev of the wrong file.

This skill is the hard stop. **When the user says any of the trigger words above, run this checklist before answering anything else.**

---

## Step 1: Re-read the Rulebook

Open `Project/rulebook.md`. Re-load at minimum:

- **Â§1** â€” AI Session Start Checklist
- **Â§2** â€” Surgical Change Rule
- The **Known Issues** section (whichever Â§ number it lives at in this project)
- The **Session Log Protocol** + **Fix Log Protocol** sections
- Any section the compaction summary flagged as relevant to the current task

**Output:** "Rulebook re-read. Sections relevant to the current task: [list]."

---

## Step 2: Re-read the Fix Log

Open `Project/logs/fix_log.md`. Read **all of today's entries** plus the last 5 entries from prior days.

This re-establishes:

- What's already been fixed in this session â€” so you don't propose to "fix" it again
- What was tried and didn't work â€” so you don't repeat a failed approach
- The next available `FIX-YYYY-MM-DD-NNN` sequence number

**Output:** "Fix log re-read. Today's entries: [FIX-NNN list]. Next sequence: FIX-YYYY-MM-DD-NNN."

---

## Step 3: Re-read Today's Session Log

Open `Project/sessions/session-YYYY-MM-DD.md` (replace with actual date). Skim every line â€” it's short by design. This is the raw shorthand of what happened pre-compaction.

If the file doesn't exist, the previous session never opened it. Create it now and log the compaction event:
```
HH:MM  REFRESH  context compacted, re-syncing
```

**Output:** "Session log re-read â€” N entries. Last activity: [last line]."

---

## Step 4: Re-read the Active Files

The compaction summary should list which files were being touched. For **each** file mentioned:

1. **Read it with the Read tool.** Do not trust your memory or the summary's snippet â€” both are lossy.
2. Note the rev number from the file header.
3. If the summary said the rev was X but the current rev is Y, the file moved while you weren't looking â€” flag it.
4. If the summary mentions an edit you "just made" but the file doesn't reflect it, the edit never landed â€” flag it.

**Output:** "Active files re-verified: [path Â· current rev Â· matches summary?]"

---

## Step 5: Re-read the Roadmap (if relevant)

If the in-progress task is roadmap-driven (sprint items, issue tracker IDs, weekly goals):

1. Open `Project/roadmap.md` â€” confirm the current item's status (open / in-progress / done / blocked).
2. If working out of a per-user folder (`Project/team/<name>/`), re-scan that folder for items checked off mid-session.

**Output:** "Roadmap re-checked. Current item: [ID] â€” status: [open / in-progress / done]."

---

## Step 6: Restate Your Position to the User

In one short paragraph, tell the user:

- What task is in progress
- What's been done already (from session log + fix log + active file revs)
- What's still pending
- What the next concrete action is

**Wait for the user to confirm before doing anything else.** The point of refresh is to re-sync with reality, not to barrel ahead based on a half-remembered plan.

---

## The Refresh Rule

**If you didn't open the file with the Read tool after the compaction, you don't actually know what's in it.**

Compaction summaries are paraphrases. Treat every file path that appears in the summary as "needs to be re-read before touching." Treat every rev number in the summary as "needs to be verified against the file header." Treat every claim of "we already fixed X" as "needs to be verified against the fix log and the actual file."

**If you skip refresh after a compaction, you will:**

- Edit a file at the wrong rev
- Re-attempt a fix that already failed and was logged in the "Tried" field
- Re-write a FIX entry that already exists
- Drift from what the user actually asked for

The refresh skill takes about 30 seconds to run. The drift it prevents takes hours to clean up.
