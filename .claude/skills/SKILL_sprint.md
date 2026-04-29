---
name: sprint
description: "Use this skill to generate a weekly sprint plan for {{PROJECT_NAME}}. Triggers: 'generate a sprint', 'plan the sprint', 'write sprint files', 'what should we work on this week', 'create the weekly plan', or any time the team needs a structured list of tasks for an upcoming sprint. Reads the roadmap, checks what's open, and produces per-engineer task files with clear ownership and dependencies."
---

# Sprint Planner — {{PROJECT_NAME}}

## Purpose

Generate clear, actionable weekly sprint files from the roadmap. Each sprint file is assigned to a specific engineer (or role), contains only their tasks, and has explicit dependencies and definition-of-done criteria.

A sprint file is not a wish list — it is a commitment. Every task in it should be completable in the sprint week. If it can't be done in the week, break it smaller.

---

## Step 1: Read the Current State

Before planning anything:

1. Read `roadmap.md` — identify all items with status `📋 PLANNED` or `🔧 IN PROGRESS`
2. Read `Project/rulebook.md §8` — note any open known issues that should be sprint items
3. Read the most recent `Project/sessions/session-summary-*.md` — what did last session leave unfinished?
4. Read any existing sprint files in `Project/team/` — what was deferred from last sprint?

**Identify:**
- Carry-forwards from last sprint (incomplete tasks)
- Open P1 items from the roadmap (these take priority)
- P2 items that are unblocked and ready to start
- Any known issue at P1 priority

---

## Step 2: Assess Team Structure

Ask the user (or infer from context):
- How many engineers are working this sprint?
- What are their roles? (e.g. backend, frontend, full-stack, designer)
- What is each engineer's ownership boundary? (e.g. "A owns actions + API, B owns pages + components")
- Are there any constraints? (e.g. "B is out Wednesday", "blocked on third-party API")

If working solo, generate a single sprint file. If working with multiple engineers, generate one file per role.

---

## Step 3: Size the Sprint

**Principles:**
- A sprint is one week (Mon–Fri, roughly 8 productive hours per day)
- Each task should take 1–4 hours to complete — if longer, break it down
- Allow 20% buffer for integration, reviews, and unexpected issues
- Do not overfill — 5 well-defined tasks is better than 10 vague ones
- Carry-forwards from last sprint come first, before new work

**Rough capacity per engineer per week:** 8–12 substantial tasks depending on complexity.

---

## Step 4: Generate Sprint Files

Create one file per engineer at `Project/team/[name]/sprint-week[N]-engineer-[role].md`.

**File format:**

```markdown
# Engineer [Role] ([Name]) — Week [N] Sprint
**Role:** [role description]
**Owns:** [folders/modules this engineer is responsible for]
**Never touch:** [folders/modules owned by other engineers]
**Generated:** YYYY-MM-DD | Rev: 1.0

---

## Sprint Context

**Last sprint completed:**
- [item] ✅
- [item] ✅

**Carries forward:**
- [incomplete item from last sprint]

**New this week:**
- [roadmap item ID + one-line description]

---

## [Day] — [Theme for the day]

### Task [ID]: [Task Name] ([roadmap ID if applicable])
**File(s):** `path/to/file.ts` (existing) OR `path/to/new-file.ts` (new)
**What the problem is:** [One sentence describing the current broken/missing state]
**What to build:**
1. [Specific, verifiable step]
2. [Specific, verifiable step]
3. [Specific, verifiable step]
**Dependencies:** [What must be done by another engineer first, or 'None']

---

## Coordination Points

| Day | [Engineer A] → [Engineer B] | [Engineer B] → [Engineer A] |
|-----|----------------------------|----------------------------|
| Mon | [what A needs to share] | [what B needs to share] |

---

## Definition of Done

- [ ] [Specific, testable outcome]
- [ ] [Specific, testable outcome]
- [ ] `tsc --noEmit` passes with 0 new errors (if TypeScript project)
- [ ] Changes committed and pushed
```

---

## Step 5: Rules for Good Sprint Tasks

**A good task has:**
- One clear owner
- A specific file path (not "somewhere in the codebase")
- A "what the problem is" — the current broken/missing state in one sentence
- Numbered, verifiable build steps — not vague instructions
- An explicit dependency declaration (even if "None")

**A bad task is:**
- "Fix the reports page" — too vague
- "Improve performance" — not measurable
- "Clean up the code" — not a sprint task
- A task that touches another engineer's ownership boundary without a coordination note

---

## Step 5.5: Rule Compliance Gate ⛔

**Every sprint task must be validated against the rulebook before it is added to a sprint file.**

For each generated task:

1. **Read `Project/rulebook.md`** — identify every rule that applies to the files the task will touch.
2. **Check the Protected Files list** — if the task modifies any file listed in §34 (Layout File Protection) or any file with a `🔒` banner, flag it immediately.
3. **Check DataPageLayout rules** — if the task changes the visual layout, column order, button placement, or behavior of any DataPageLayout list view, flag it.
4. **Check the Surgical Change Rule (§2)** — if the task modifies working code that the roadmap item didn't explicitly request changed, flag it.

**If a task violates ANY rule:**

- Mark it with `⛔ REQUIRES OWNER APPROVAL` in the sprint file
- Add a `**Rule conflict:**` line citing the specific rule (e.g., "§34 — modifies protected layout file")
- **Do NOT include the task as a normal sprint item.** It goes into a separate "Requires Approval" section at the bottom of the sprint file
- The owner ({{OWNER_NAME}}) must explicitly approve the task before any session executes it

**Sprint file format for flagged tasks:**

```markdown
## ⛔ Requires Owner Approval

### Task [ID]: [Task Name]
**Rule conflict:** §[N] — [one-line explanation of which rule this violates]
**What it would change:** [specific files and changes]
**Why it was flagged:** [AI's reasoning for why this conflicts with the rule]
**Owner decision:** [ ] Approved — proceed  [ ] Rejected — remove from sprint  [ ] Modified — see notes
**Owner notes:** (left blank for owner to fill in)
```

**The AI must never execute a flagged task without seeing an explicit `[x] Approved` checkbox from the owner.**

---

## Step 6: Confirm with the User

Before saving the files, present the sprint plan as a summary:
- List each engineer and their task count
- Highlight any dependencies between engineers
- Flag any task that looks too large for one sprint
- Flag any known blocker (external dependency, missing API key, etc.)
- **List every task in the "Requires Approval" section with its rule conflict — these need explicit sign-off**

Wait for owner confirmation before writing the files.

---

## Step 7: Write the Files and Commit

```bash
git add Project/team/[name]/sprint-week[N]-*.md
git commit -m "docs(sprint): generate week [N] sprint files — [N] engineers, [N] tasks"
git push origin main
```

---

## Sprint Retrospective (optional — run at end of sprint)

If the user asks for a sprint retrospective:

1. Read all sprint files for the completed week
2. For each task: was it done? Partially done? Deferred?
3. Generate a `Project/logs/sprint-week[N]-retrospective.md`:
   - Completion rate (tasks done / tasks planned)
   - What caused deferrals (underscoped? blocked? discovered hidden complexity?)
   - Carry-forwards with updated descriptions for next sprint
   - One process improvement for next sprint

Keep retrospectives short — half a page is enough. The goal is to get marginally better each sprint, not to produce a novel.
