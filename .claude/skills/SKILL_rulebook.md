---
name: rulebook
description: "Use this skill whenever the rulebook needs to be updated, amended, or expanded for The Great Email App. Triggers: 'update the rulebook', 'add this to the rulebook', 'add a new rule', 'the rulebook is out of date', 'document this pattern', 'we learned something that should be a rule', or any time a new pattern, fix, or architectural decision should be permanently documented."
---

# Rulebook Update Skill â€” The Great Email App

## Purpose

The rulebook is the AI's long-term memory and the single source of truth for how this project is built. This skill ensures that when a rule is added or changed, it is:
- Integrated into the right section (not just appended at the bottom)
- Written in the right format (concise, actionable, not prose)
- Not duplicating or contradicting an existing rule
- Versioned properly

**The rulebook must evolve as the project evolves.** Every time a pattern is proven, a bug reveals a gap, or a decision is made â€” that is information the rulebook should capture so no future session has to re-learn it.

---

## Step 1: Identify the Change

Before touching the file, answer:

1. **What is the new rule?** State it in one sentence.
2. **Which section does it belong in?** Check the table of contents.
3. **Is this a correction, expansion, or net-new addition?**
   - Correction â†’ replace the existing text
   - Expansion â†’ add to the existing section
   - Net-new â†’ add a new entry or subsection
4. **Does it contradict anything already in the rulebook?** If yes, resolve the conflict.
5. **Does it affect Known Issues (Â§8)?** If yes, update that table too.

---

## Step 2: Find the Right Section

| Topic | Section |
|-------|---------|
| Session checklist, output format, priorities | Â§1 |
| Surgical change rule, laws, documentation requirements | Â§2 |
| Folder structure | Â§3 |
| File dating, authorship | Â§4 |
| Routes, security, auth gates | Â§5 |
| Database schema, core tables | Â§6 |
| Auth flow | Â§7 |
| Known bugs, limitations, tech debt | Â§8 |
| CRUD patterns, return shapes, validation | Â§9 |
| Components, UI/UX rules | Â§10 |
| Error handling | Â§11 |
| Environment variables | Â§12 |
| CCCP protocol | Â§13 |
| Query limits, data caps | Â§14 |
| Session log protocol | Â§15 |
| Lessons learned | Â§16 |

If the topic doesn't fit any existing section, propose a new section number and add it to the table of contents.

---

## Step 3: Write the Rule

**Format rules:**
- One rule = one sentence or one code block. Not a paragraph.
- Use `**bold**` for the rule itself, plain text for explanation.
- Use code blocks for examples â€” real code, not pseudocode.
- Use tables for reference data (limits, roles, status values, etc.)
- No filler words. No "it is important to note that". Just the rule.

**Good:**
```
**Every list query must have an explicit .limit() clause.** PostgREST default is 1000 rows â€” 
queries without a limit silently truncate results and cause KPIs to be wrong.
```

**Bad:**
```
It is important that developers remember to always make sure that queries include a limit 
because otherwise the database might not return all the results that are expected.
```

---

## Step 4: Apply the Change

1. Read the current rulebook section
2. Make the targeted edit â€” do not rewrite surrounding text unless it's wrong
3. Update the rulebook version header:
   - Bump `Revision N` â†’ `Revision N+1`
   - Update the date
4. If a Known Issue was resolved â†’ mark `âœ… Fixed YYYY-MM-DD` in Â§8
5. If a new Known Issue â†’ add a row to Â§8

---

## Step 5: Document the Change in Session Log

Append to `Project/sessions/session-YYYY-MM-DD.md`:
```
HH:MM  RULEBOOK  Â§N updated â€” [one-line description of what changed]
```

---

## Step 6: Commit

```bash
git add Project/rulebook.md
git commit -m "docs(rulebook): [description of rule added/changed]"
git push origin main
```

---

## Principles for a Good Rulebook

**It should be a decision log, not a tutorial.** Assume the reader knows how to code. Document *what we decided* and *why*, not *how to do basic things*.

**Every rule should have a reason.** If you can't explain why a rule exists in one sentence, it probably shouldn't be a rule.

**Rules come from experience.** The best additions are rules that prevent a specific bug or wasted session that already happened. "We got burned by X so now we always do Y."

**Keep it lean.** A rulebook nobody reads is worthless. If it's too long, nobody reads it. When adding, consider whether something already in the rulebook can be removed or consolidated.

**The session log feeds the rulebook.** Patterns that appear 2+ times in session logs are candidates for rulebook rules. Single occurrences go in Â§16 Lessons Learned until they prove out.
