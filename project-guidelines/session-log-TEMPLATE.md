# Session Log — YYYY-MM-DD
**Project:** {{PROJECT_NAME}}  
**Engineer:** {{OWNER_NAME}} + {{AI_MODEL}}

Raw shorthand notes. **Local only — gitignored.** Formatted EOD version → `session-summary-YYYY-MM-DD.md` (committed).

---

## Format

One line per event. Append in real time — not in batches at the end.

```
HH:MM  FIX-YYYY-MM-DD-NNN  <one-line summary of fix applied>
HH:MM  INVESTIGATE          <what was found during investigation>
HH:MM  BLOCKER              <what is blocking and why>
HH:MM  COMMIT  <sha>        <commit subject line>
HH:MM  DECISION             <architectural or product decision made>
HH:MM  RULEBOOK             §N updated — <what changed>
HH:MM  DEPLOY               <what was deployed and to where>
```

---

## Log

_(Start appending here — one line per event as they happen)_

