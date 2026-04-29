# {{PROJECT_NAME}} — Master Roadmap
**Created:** YYYY-MM-DD | **Updated:** YYYY-MM-DD  
**Stack:** {{STACK}}  
**Owner:** {{OWNER_NAME}}  
**Vision:** _(One sentence: what this project does and why it's worth building.)_

---

## Vision Statement

_(2–3 sentences. Who is the user? What problem does this solve? What makes it worth building over existing alternatives?)_

---

## Priority Tiers

- 🔴 **P0 — Launch Blockers** (must ship before first real user)
- 🟠 **P1 — High Impact** (operational value, ship within first month)
- 🟡 **P2 — Competitive Parity** (matches market leaders, 1–3 months)
- 🟢 **P3 — Market Differentiators** (beats competition, 3–6 months)

---

## 🔴 P0 — Launch Blockers

These must be done before the first real user touches the product.

| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P0-1 | Auth — login / logout / session management | 📋 PLANNED | |
| P0-2 | Core data model — primary tables created and seeded | 📋 PLANNED | |
| P0-3 | Basic CRUD on primary entity | 📋 PLANNED | |
| P0-4 | Role-based access control (basic) | 📋 PLANNED | |
| P0-5 | Environment variables validated at startup | 📋 PLANNED | |
| P0-6 | Error boundaries / error pages | 📋 PLANNED | |
| P0-7 | Rate limiting on all public endpoints | 📋 PLANNED | |
| P0-8 | Basic input validation (server-side) | 📋 PLANNED | |

**Status key:** `📋 PLANNED` · `🔧 IN PROGRESS` · `⚠️ PARTIAL` · `✅ DONE`

---

## 🟠 P1 — High Impact

Core workflows that make the product actually usable.

| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P1-1 | _(fill in)_ | 📋 PLANNED | |
| P1-2 | _(fill in)_ | 📋 PLANNED | |

---

## 🟡 P2 — Competitive Parity

Features that match what alternatives already offer.

### Category 1
| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P2-1 | _(fill in)_ | 📋 PLANNED | |

### Technical Debt
| ID | Item | Status | Notes |
|----|------|--------|-------|
| P2-TD-1 | TypeScript strict mode | 📋 PLANNED | Enable after initial build stabilizes |
| P2-TD-2 | Test coverage — critical paths | 📋 PLANNED | Auth, payment, data mutation |
| P2-TD-3 | Dependency audit + cleanup | 📋 PLANNED | Remove unused packages |

---

## 🟢 P3 — Market Differentiators

Features that make this product genuinely better than alternatives.

### AI & Automation
| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P3-1 | _(fill in)_ | 📋 PLANNED | |

### Integrations
| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P3-2 | _(fill in)_ | 📋 PLANNED | |

### Growth Features
| ID | Feature | Status | Notes |
|----|---------|--------|-------|
| P3-3 | _(fill in)_ | 📋 PLANNED | |

---

## Recommended Execution Order

### Week 1 — Foundation
1. P0-1 through P0-8 — all launch blockers

### Week 2–3 — Core Workflows
_(P1 items — fill in as sprint files are generated)_

### Month 2 — Competitive Parity
_(P2 items)_

### Month 3+ — Differentiators
_(P3 items)_

---

## Decision Log

Decisions that required owner input. AI must not re-litigate these.

| Decision | Context | Made By | Date | Status |
|----------|---------|---------|------|--------|
| _(e.g. Supabase vs Firebase)_ | _(why this came up)_ | {{OWNER_NAME}} | YYYY-MM-DD | DECIDED |

---

## Known Tech Debt Backlog

Items that were knowingly deferred — not forgotten, just not yet scheduled.

| Item | Why Deferred | Target |
|------|-------------|--------|
| _(fill in)_ | _(fill in)_ | _(sprint or milestone)_ |

---

## Site / App Health Tracker

Updated each sprint. Gives a snapshot of current risk.

| Category | Count | Notes |
|----------|-------|-------|
| 🔴 Critical (data loss / security) | 0 | |
| 🟠 High (broken workflow) | 0 | |
| 🟡 Medium (degraded UX) | 0 | |
| ⚪ Low / housekeeping | 0 | |
| **Total open** | **0** | |

---

## Competitive Analysis

| Competitor | What they do well | Our advantage |
|------------|------------------|--------------|
| _(fill in)_ | _(fill in)_ | _(fill in)_ |

---

## Shipped Log

As items ship, move them here from the sections above. Keeps the active roadmap clean.

| ID | Feature | Shipped | Notes |
|----|---------|---------|-------|
| _(fill in)_ | _(fill in)_ | YYYY-MM-DD | |
