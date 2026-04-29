# {{PROJECT_NAME}} — DEVELOPMENT RULEBOOK
**Version:** 1.0 · Revision 1  
**Stack:** {{STACK}}  
**Authority:** Mandatory for All AI + Human Contributors  
**Owner:** {{OWNER_NAME}}

> **AI:** Read the relevant sections before writing any code. Confirm file revs before modifying. Verify DB schema before any DB-touching code. Check §8 Known Issues. Apply §2 Surgical Change Rule.

---

## TABLE OF CONTENTS

1. [Priorities & Meta-Protocol](#1-priorities--meta-protocol)
2. [Laws & Surgical Change Rule](#2-laws--surgical-change-rule)
3. [Folder Structure](#3-folder-structure)
4. [File Dating & Authorship](#4-file-dating--authorship)
5. [Routes & Security](#5-routes--security)
6. [Database Schema](#6-database-schema)
7. [Auth Flow](#7-auth-flow)
8. [Known Issues](#8-known-issues)
9. [CRUD Standards](#9-crud-standards)
10. [Components & UI/UX](#10-components--uiux)
11. [Error Handling](#11-error-handling)
12. [Environment Variables](#12-environment-variables)
13. [CCCP Protocol](#13-cccp-protocol)
14. [Query & Data Limits](#14-query--data-limits)
15. [Session Log Protocol](#15-session-log-protocol)
16. [Lessons Learned](#16-lessons-learned)

---

## 1. Priorities & Meta-Protocol

**Priority order:** Security > Correctness > Speed > Scalability > Maintainability

**Output format rule:**
- Scripts for Claude Code → use **CCCP Protocol** (§13)
- All other contexts → **output full files**, never partial replacements
- Never output placeholders, stubs, TODOs, or lorem ipsum

**AI Session Start Checklist:**
- [ ] Read relevant rulebook sections for the current task
- [ ] Confirm current rev of any file being modified (read the file header)
- [ ] Verify DB schema before any DB-touching code
- [ ] Check §8 Known Issues — is this already diagnosed?
- [ ] Apply §2 Surgical Change Rule — touch only what's broken
- [ ] Open or create today's session log — `Project/sessions/session-YYYY-MM-DD.md` (see §15)

**AI Session — Ongoing:**
- After every non-trivial fix, discovery, or decision → append one line to the session log immediately
- After any bug is resolved → run `/log-fix` to append to `Project/logs/fix_log.md`
- Do NOT wait until end of session — context compaction wipes chat memory without warning
- Log files survive compaction; chat memory does not

**AI Session End:**
- [ ] Flush session log — convert bullets to proper rulebook entries / Known Issues / file comments
- [ ] Run EOD skill to generate `session-summary-YYYY-MM-DD.md`
- [ ] Commit all changes

**Stop and Verify Rule:** If a fix doesn't work on the first attempt, do NOT try a second variation. Verify the component is actually rendered on the affected page, verify the file you edited is actually imported, state what you found before proposing the next fix. Three failed attempts = stop and ask the user to provide the file contents directly.

---

## 2. Laws & Surgical Change Rule

- No placeholders, stubs, TODOs, or lorem ipsum — ever
- Read existing code before generating anything
- All files must include creation/revision date + rev number at top
- Ask before creating any new folder, route, component, action, type, table, or utility
- No new dependencies without explicit owner approval

### DOCUMENT YOUR CHANGES IN THE FILE — NO SILENT MODIFICATIONS

Any time an AI makes a non-trivial change — a limit, a filter, a default value, a workaround, a special case — **document it in that file's header comment at the time of the change.** Not in chat. In the file.

If the change relates to a rulebook section, reference it inline:
```ts
// NOTE: limit set to 5000 per §14 — DB default cap is 1000
// NOTE: filter excludes soft-deleted rows — is_active=true enforced here
```

**The rule in one sentence:** If a future developer or AI session would need to read a chat log to understand why you did something, you didn't document it enough.

### THE MOST IMPORTANT LAW — SURGICAL CHANGE RULE

> When the user says something is working and only X needs fixing: **change ONLY X.** Not the imports. Not the types. Not the flow. Not the naming. **ONLY the specific broken thing.**
>
> Rewriting working code to "clean it up" while fixing a bug is **forbidden.** Read the error message. Fix only what the error describes.

Before modifying ANY file to fix a UI issue, verify the component actually renders on the affected page:
```bash
grep -r "ComponentName" src/ --include="*.tsx" --include="*.ts"
```

---

## 3. Folder Structure

> **Replace this section** with your actual folder tree once the project structure is decided.

```
{{STACK_FOLDER_STRUCTURE}}
```

**Universal rules (apply to all stacks):**
- Config/env files at project root — never inside `src/`
- Shared utilities in `lib/` or `utils/` — never duplicated across modules
- Types in a dedicated `types/` folder — never inline in component files
- Tests colocated with the file they test, or in a top-level `tests/` folder
- No business logic in UI components — keep components presentational

---

## 4. File Dating & Authorship

Every generated or modified file must include at the very top:

```
// FILE: path/to/file.ext
// Created: YYYY-MM-DD | Revised: YYYY-MM-DD | Rev: N
// Changed by: {{AI_MODEL}} on behalf of {{OWNER_NAME}}
```

**Authored-By Rule:**
- Every AI-modified file must have a `Changed by:` line
- Use the actual model name (e.g. `Claude Sonnet 4.6`) — never just "Claude"
- Update the line on subsequent edits — do not stack multiple lines
- Human developers may use their own name or omit the line

**Non-Obvious Changes Must Be In-File:**
Every non-trivial decision — a row limit, a default value, a filter, a workaround — must be explained in the file header or inline comment at the exact line. Examples:

```
// NOTE: .limit(5000) — DB default cap is 1000; table has 3000+ rows
// NOTE: is_active filter — soft-delete pattern, never hard-delete rows
// NOTE: retry logic here — third-party API returns 429 on first call under load
```

If it would take a future developer more than 30 seconds to understand why a line exists, comment it.

---

## 5. Routes & Security

> **Fill in** your route map, auth gates, and public vs protected routes.

**Universal rules:**
- All non-public routes must have server-side auth verification — never trust client-only guards
- Rate limit all public API endpoints — even low-traffic ones
- Never expose internal IDs or tokens in client-accessible URLs without validation
- Validate all inputs server-side regardless of client-side validation

**Auth pattern for this project:** `{{AUTH_PATTERN}}`

| Route Pattern | Auth Required | Notes |
|--------------|--------------|-------|
| _(fill in)_ | _(fill in)_ | _(fill in)_ |

---

## 6. Database Schema

> **Fill in** your core tables, key columns, and constraints once the schema is defined.

**DB platform:** `{{DB}}`

**Universal rules:**
- Soft-delete pattern preferred over hard delete — add `is_active` / `deleted_at` columns
- All tables get `created_at` (default `now()`) and `updated_at` (trigger-updated)
- Foreign keys must have ON DELETE behavior explicitly set — never leave it implicit
- Never run raw SQL in application code without parameterization
- Schema changes via migration files only — never edit DB directly in production

### Core Tables

| Table | Key Columns | Notes |
|-------|------------|-------|
| _(fill in)_ | _(fill in)_ | _(fill in)_ |

---

## 7. Auth Flow

> **Fill in** your authentication pattern once decided.

**Pattern:** `{{AUTH_PATTERN}}`

**Universal rules:**
- Never store passwords in plain text — ever
- Session tokens must be invalidated on logout server-side
- Magic links / one-time tokens must have expiry and single-use enforcement
- Never trust user-supplied role or permission claims without server-side verification
- Auth checks belong in server-side middleware/guards, not component-level `if` statements

---

## 8. Known Issues

Track all known bugs, limitations, and technical debt here. AI must check this section before starting any task.

| # | Priority | Description | Status |
|---|----------|-------------|--------|
| 1 | — | _(first known issue)_ | Open |

**Priority legend:**
- **P1** — Breaking or data-loss risk. Fix immediately.
- **P2** — Operational impact. Fix this sprint.
- **P3** — Polish or edge case. Fix when convenient.

**Status values:** `Open` · `⚠️ Partial` · `✅ Fixed YYYY-MM-DD` · `❌ Won't Fix (reason)`

---

## 9. CRUD Standards

Every data-fetching or mutation function must follow this pattern:

**Return shape — always discriminated union:**
```ts
| { success: true;  data: T }
| { success: false; error: string }
```

**Mandatory checklist per function:**
- [ ] Auth check — verify the caller is authenticated
- [ ] Scope check — verify the caller owns the resource (tenant/user isolation)
- [ ] Input validation — Zod or equivalent before touching the DB
- [ ] Error handling — try/catch, never let DB errors bubble raw to the client
- [ ] Explicit column selection — never `SELECT *` in production queries
- [ ] Row limit — all list queries must have an explicit limit (see §14)
- [ ] Return type — typed, never `any`

**Example pattern:**
```ts
export async function getItems(params: GetItemsInput): Promise<GetItemsResult> {
  try {
    const user = await getCurrentUser()
    if (!user) return { success: false, error: 'Unauthorized' }

    const parsed = GetItemsSchema.safeParse(params)
    if (!parsed.success) return { success: false, error: parsed.error.message }

    const { data, error } = await db
      .from('items')
      .select('id, name, created_at')
      .eq('owner_id', user.id)
      .limit(1000) // §14
      .order('created_at', { ascending: false })

    if (error) throw error
    return { success: true, data: data ?? [] }
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Unknown error'
    console.error('[getItems]', message)
    return { success: false, error: message }
  }
}
```

---

## 10. Components & UI/UX

> **Fill in** your component library and design system choices.

**Component library:** _(e.g. shadcn/ui, Material UI, Tailwind only)_  
**Design tokens:** _(e.g. CSS custom properties, Tailwind theme, design system variables)_

**Universal rules:**
- No business logic in UI components — keep them presentational
- No data fetching inside components using `useEffect` — fetch in server components or dedicated hooks
- Loading states: every async operation must have a loading indicator
- Error states: every async operation must handle and display errors
- Empty states: every list/table must have a designed empty state — never just nothing
- Never hardcode colors, spacing, or font sizes — use design tokens
- All interactive elements must have accessible labels (`aria-label`, `title`, or visible text)

---

## 11. Error Handling

**Hierarchy:**
1. Validate inputs before the operation (Zod / schema validation)
2. Catch errors at the service/action layer — never let them propagate raw
3. Log with context: `console.error('[functionName] message', err)`
4. Return structured error to the caller — never throw across async boundaries
5. Display user-friendly messages — never show raw DB errors or stack traces to users

**Never:**
- Swallow errors silently (`catch {}` with empty body)
- Display raw error messages from DB or third-party APIs to users
- Retry failed operations without a delay and a maximum attempt count

---

## 12. Environment Variables

All required environment variables must be documented here. The app must fail loudly (not silently) if any required variable is missing.

| Variable | Required | Description |
|----------|----------|-------------|
| _(fill in)_ | Yes/No | _(fill in)_ |

**Rules:**
- Never commit `.env` files — only `.env.example` with dummy values
- Validate all required env vars at startup — fail fast with a clear error message
- Client-side env vars must be explicitly prefixed (e.g. `NEXT_PUBLIC_` in Next.js)
- Rotate secrets immediately if they appear in a commit or log

---

## 13. CCCP Protocol

**CCCP = Claude Code Change Proposal.** Use this format when generating a script for Claude Code (VS Code) to apply to the codebase.

```
=== CCCP ===
ID: CCCP-NNN
Title: Short description
Files: path/to/file1.ts, path/to/file2.tsx
Risk: Low / Medium / High
Description:
  What is being changed and why.

--- FILE: path/to/file1.ts ---
// Full file contents here
// Never partial — always the complete file
```

**Rules:**
- Never use CCCP for conversational responses — only for code that will be applied by Claude Code
- Always output the complete file, never diffs or partial replacements
- Include the file header with Created/Revised/Rev/Changed-by lines
- State the risk level — if High, explain why and what to verify after applying

---

## 14. Query & Data Limits

**The silent cap problem:** Most databases and ORMs have a default result limit (e.g. PostgREST/Supabase defaults to 1000 rows). Queries without an explicit limit will silently return truncated results. This causes KPIs to be wrong, reports to undercount, and searches to miss records.

**Mandatory rule:** Every list query must have an explicit `.limit()` or `LIMIT` clause.

**Sizing guide:**
| Use Case | Suggested Limit |
|----------|----------------|
| Paginated list (one page) | 25–100 |
| Autocomplete / typeahead | 10–20 |
| Full table for client-side filter | 5,000–50,000 |
| Report / aggregate (all time) | 100,000+ with pagination |
| Single-record lookup | No limit needed |
| COUNT query (head: true) | No limit needed |

**Document every limit inline:**
```ts
.limit(5000) // §14 — table has 3000+ rows; PostgREST default is 1000
```

---

## 15. Session Log Protocol

**Purpose:** The session log is the AI's short-term memory that survives context compaction. Chat memory does not.

**File:** `Project/sessions/session-YYYY-MM-DD.md`  
**Gitignored:** Yes — raw shorthand, local only  
**Consumed by:** EOD skill → generates `session-summary-YYYY-MM-DD.md` (committed)

**Format — one line per event:**
```
HH:MM  FIX-YYYY-MM-DD-NNN  <one-line summary of fix>
HH:MM  INVESTIGATE  <what was found>
HH:MM  BLOCKER  <what is blocking and why>
HH:MM  COMMIT  <sha>  <commit subject>
HH:MM  DECISION  <architectural or product decision made>
```

**Rules:**
- Open or create the log at the start of every session (preflight step 0)
- Append after every meaningful unit of work — not in batches at the end
- Keep entries short — one line per event, expand in EOD summary if needed
- If the log doesn't exist at EOD, the summary will be missing half the work

---

## 16. Fix Log Protocol

**Purpose:** The session log is throw-away shorthand for one calendar day. The fix log is the project's permanent troubleshooting memory. Every bug that gets diagnosed and fixed gets one structured entry — symptom, what didn't work, what did, and which files moved. **This is what stops a future session (or a future engineer) from re-running a debugging path that already failed.**

**File:** `Project/logs/fix_log.md`
**Gitignored:** **No — committed to the repo.** This is institutional memory, not scratch paper.
**Created from:** `project-guidelines/fix-log-TEMPLATE.md` at project bootstrap.
**Updated by:** `/log-fix` slash command after each resolved bug, or manually using the format below.

### Why a separate file from the session log

| | Session log | Fix log |
|---|---|---|
| Filename | `Project/sessions/session-YYYY-MM-DD.md` | `Project/logs/fix_log.md` |
| Scope | One calendar day | Whole project |
| Format | Free-form shorthand bullets | Strict structured entries |
| Gitignored | Yes (local only) | **No (committed)** |
| Lifespan | Days (consumed by EOD skill, then archived) | Forever |
| Purpose | "What did I do today" | "Has this bug been seen before, and what worked?" |

The session log is for **today's** memory. The fix log is for **every future session's** memory.

### Entry format

```markdown
---

## FIX-YYYY-MM-DD-NNN

**Area:** [module or page affected, e.g. "Inventory", "Auth", "Marketing/Hero"]
**Status:** ✅ Fixed / ⚠️ Open / ❌ Won't Fix / 🔁 Regressed
**Priority:** P1 / P2 / P3

**Symptom:** [What the user sees, in their words.]

**Replicate:**
1. [Step]
2. [Step]

**Root cause:** [One sentence: what in the code caused this.]

**Tried:**
- [Approach that didn't work, and why — this field is the whole point of the log]

**Fix:** [What was changed. File · old → new.]

**Files changed:**
- `path/to/file.ts` (Rev N → N+1)

**Rulebook:** [§N if a rulebook section covers this pattern. "None" if not.]

**Session:** YYYY-MM-DD
**Commit:** `xxxxxxx`
```

### Rules

1. **Every bug-class change gets an entry.** Features and refactors do not — those go to the session log, the changelog, or roadmap notes.
2. **Append-only.** Never delete entries. If a fix regresses, mark the original `🔁 Regressed` and add a new entry that points back to it.
3. **The "Tried" field is mandatory.** "Nothing — fixed on first attempt" is a valid value, but the field itself is never blank. Future sessions read this field to avoid re-running failed experiments.
4. **NNN is a daily zero-padded sequence** — `001`, `002`, `003`. Resets each calendar day.
5. **Read the fix log before touching a file.** Preflight Step 3 requires it. If a similar entry already exists, that entry's **Tried** and **Fix** fields are required reading before proposing anything new.
6. **Committed to git.** Never `.gitignore` this file. It's the audit trail.

### When to add an entry

Add a fix log entry when you:

- Resolve a reported bug
- Find and patch a silent failure (data not loading, wrong totals, missing records)
- Work around a third-party limitation
- Fix a regression — even a small one
- Discover a root cause that took more than one attempt to find

Do **not** add entries for:

- New features (those go in commits + roadmap)
- Cosmetic tweaks that aren't fixing a defect
- Refactors that don't change behavior
- Doc-only changes

### The "Tried — didn't work" discipline

The single most valuable field in the entire log. Without it, the next AI session will:

- Try the same wrong approach you already tried
- Burn an hour rediscovering it doesn't work
- Eventually arrive at the right answer that's already documented

**Always write down what didn't work, even if it feels embarrassing or obvious.** The point of the log is that it's read by someone (or something) that wasn't in the room when the debugging happened.

---

## 17. Lessons Learned

> Add entries here as the project reveals patterns that should be documented.
> Format: **date · category · lesson**

| Date | Category | Lesson |
|------|----------|--------|
| _(fill in)_ | _(e.g. DB, Auth, UI, API)_ | _(one-sentence lesson)_ |
