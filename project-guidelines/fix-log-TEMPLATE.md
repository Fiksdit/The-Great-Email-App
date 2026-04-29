# Fix Log — {{PROJECT_NAME}}

Persistent audit trail of every bug diagnosed and fixed. Committed to git — survives context compaction and session changes.

**Format:** `FIX-YYYY-MM-DD-NNN` where NNN is a zero-padded sequence number per day.  
**Status values:** `✅ Fixed` · `⚠️ Open` · `❌ Won't Fix` · `🔁 Regressed`

---

## How to Add an Entry

Run the `/log-fix` skill, or manually append using the template below. Always fill in every field — incomplete entries are useless when debugging a regression weeks later.

```markdown
---

## FIX-YYYY-MM-DD-001

**Area:** [module or page affected, e.g. "Inventory", "Auth", "Reports"]  
**Status:** ✅ Fixed / ⚠️ Open  
**Priority:** P1 / P2 / P3  

**Symptom:** [What the user sees. Exactly what went wrong, from the user's perspective.]

**Replicate:**
1. [Step to reproduce]
2. [Step to reproduce]

**Root cause:** [One sentence: what in the code caused this.]

**Tried:**
- [Thing tried that didn't work, and why]

**Fix:** [What was changed. Be specific — file, line, what was wrong, what it was changed to.]

**Files changed:**
- `path/to/file.ts` (Rev N → N+1)

**Rulebook:** [§N if a rulebook section covers this pattern. "None" if not.]

**Session:** YYYY-MM-DD  
**Commit:** `xxxxxxx`
```

---

## Log

_(Entries appended here, newest at bottom)_

---

## FIX-YYYY-MM-DD-001

**Area:** _(fill in)_  
**Status:** ⚠️ Open  
**Priority:** P2  

**Symptom:** _(First entry — fill in when the first bug is found.)_

**Replicate:**
1. _(step)_

**Root cause:** _(fill in)_

**Tried:** Nothing yet.

**Fix:** _(fill in when resolved)_

**Files changed:** _(fill in)_

**Rulebook:** None  
**Session:** YYYY-MM-DD  
**Commit:** _(fill in)_
