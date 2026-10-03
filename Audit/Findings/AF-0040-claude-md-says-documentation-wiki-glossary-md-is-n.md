---
id: AF-0040
title: CLAUDE.md says Documentation/Wiki/Glossary.md is 'not yet written' but it exists
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:CLAUDE.md:Glossary:false-statement
task: none
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
closed:
closed-by:
---
# AF-0040 - CLAUDE.md says Documentation/Wiki/Glossary.md is 'not yet written' but it exists

## Summary

Low finding from the truthfulness auditor at `CLAUDE.md:228`: CLAUDE.md says Documentation/Wiki/Glossary.md is 'not yet written' but it exists. Reported by an auditor flagged unreliable in 2026-10-03_1459.md.

## Evidence

Location: `CLAUDE.md:228`

CLAUDE.md line 228: 'Documentation/Wiki/Glossary.md (not yet written; align-and-document starts it on its first run)'. The file exists, begins '# Glossary' and has a term table with a 'Name in code' column.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern 'not yet written'; Test-Path Documentation/Wiki/Glossary.md
```

- Expected: No 'not yet written' claim while the file exists.
- Actual: CLAUDE.md:228 matches 'not yet written' and Test-Path returns True.

## Re-audits

## Log

- 2026-10-03: filed proposed.
