# Project Guidelines — Enterprise App Starter Kit

**Maintainer:** James Reed  
**Source project:** Fiks'd IT (`firebase/`)  
**Last updated:** 2026-04-11  
**Purpose:** Drop this folder into any new project to get enterprise-quality AI-assisted development from day one.

---

## What This Is

Every project we build needs the same foundation:
- A **rulebook** the AI reads before every session — mandatory rules, patterns, and decisions
- A **roadmap** that tracks features from idea → shipped, with priority tiers
- **Skills** the AI runs at the start and end of every session, and when sprints are generated
- **Session + fix logs** that survive context compaction and create an audit trail

This folder contains templates for all of it. Copy, find-replace the tokens, and you have a production-grade project foundation before writing a single line of code.

---

## Files in This Folder

| File | Purpose | Copy to |
|------|---------|---------|
| `RULEBOOK_TEMPLATE.md` | Master rulebook. AI reads this before every task. | `Project/rulebook.md` |
| `ROADMAP_TEMPLATE.md` | P0→P3 feature roadmap with decision log. | `Project/roadmap.md` |
| `session-log-TEMPLATE.md` | Daily session log format. One file per day, gitignored. | `Project/sessions/session-YYYY-MM-DD.md` |
| `fix-log-TEMPLATE.md` | Persistent bug fix audit log. Committed to git. | `Project/logs/fix_log.md` |
| `skills/SKILL_preflight.md` | AI checklist run before every task. | `.claude/skills/SKILL_preflight.md` |
| `skills/SKILL_eod.md` | End-of-day session summary generator. | `.claude/skills/SKILL_eod.md` |
| `skills/SKILL_rulebook.md` | Rulebook update skill — amends rules as project evolves. | `.claude/skills/SKILL_rulebook.md` |
| `skills/SKILL_sprint.md` | Sprint planner — generates per-engineer weekly task files. | `.claude/skills/SKILL_sprint.md` |

---

## Setup Checklist (New Project)

### Step 1 — Copy files
```bash
cp project-guidelines/RULEBOOK_TEMPLATE.md Project/rulebook.md
cp project-guidelines/ROADMAP_TEMPLATE.md Project/roadmap.md
mkdir -p Project/sessions Project/logs Project/team .claude/skills
cp project-guidelines/session-log-TEMPLATE.md Project/sessions/session-$(date +%Y-%m-%d).md
cp project-guidelines/fix-log-TEMPLATE.md Project/logs/fix_log.md
cp project-guidelines/skills/SKILL_preflight.md .claude/skills/SKILL_preflight.md
cp project-guidelines/skills/SKILL_eod.md .claude/skills/SKILL_eod.md
cp project-guidelines/skills/SKILL_rulebook.md .claude/skills/SKILL_rulebook.md
cp project-guidelines/skills/SKILL_sprint.md .claude/skills/SKILL_sprint.md
```

### Step 2 — Replace tokens
Find and replace every `{{TOKEN}}` placeholder:

| Token | Replace with |
|-------|-------------|
| `{{PROJECT_NAME}}` | Your project name (e.g. `Fiks'd IT`, `MyApp`) |
| `{{STACK}}` | Primary stack (e.g. `Next.js 14 + Supabase`, `Python + FastAPI`, `React Native`) |
| `{{DB}}` | Database (e.g. `Supabase/PostgreSQL`, `MongoDB`, `SQLite`) |
| `{{REPO_URL}}` | GitHub repo URL |
| `{{OWNER_NAME}}` | Owner/lead developer name |
| `{{AI_MODEL}}` | Default AI model (e.g. `Claude Sonnet 4.6`) |
| `{{STACK_FOLDER_STRUCTURE}}` | Your actual folder tree |
| `{{AUTH_PATTERN}}` | How auth works (e.g. `Supabase getUser()`, `JWT`, `session cookies`) |

### Step 3 — Add .gitignore entries
```gitignore
# AI session logs — raw shorthand, local only
Project/sessions/session-[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9].md
```

### Step 4 — Fill in the rulebook
The rulebook template ships with universal rules pre-filled. Add your project-specific rules:
- §3 Folder Structure — paste your actual folder tree
- §7 Database Schema — paste your core tables
- §9 RBAC — define your user roles
- §21 Environment Variables — list all required env vars

### Step 5 — Seed the roadmap
Add your P0 launch blockers to the roadmap. Everything else can be filled in as you go.

---

## The Core Philosophy (Why This Works)

The AI has no persistent memory between sessions. Every session starts blank. Without a rulebook it will:
- Guess at your folder structure and put files in the wrong place
- Re-litigate decisions you already made
- Skip verification steps and introduce bugs
- Miss known issues you've already diagnosed

The rulebook is the AI's long-term memory. The session log is its short-term memory. The fix log is the audit trail. The preflight skill enforces that all three are opened before any work starts.

**The session log is gitignored on purpose** — it's raw shorthand, not a deliverable. The EOD skill converts it into a clean `session-summary-YYYY-MM-DD.md` that IS committed.

---

## Evolving the Guidelines

When a new pattern emerges on any project that should apply to all projects:
1. Update the relevant template file here
2. Back-port it to active projects that need it
3. The `SKILL_rulebook` skill handles in-project rulebook updates; this folder handles cross-project propagation

**These templates are a living document.** The Fiks'd IT project is the reference implementation — patterns proven there get promoted here.

---

## Skills vs In-Project Skill Files

There are two layers of skills:
- **User-level skills** — live in `AppData/Roaming/Claude/.../skills/` — active across ALL projects
- **In-project skill files** — live in `.claude/skills/` — project-specific, committed to git, readable by the AI

The files in `project-guidelines/skills/` are designed for `.claude/skills/` (in-project). They can also be registered as user-level skills if you want them globally available.
