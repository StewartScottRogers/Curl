---
id: BL-1383
title: Fix AF-0040: CLAUDE.md says Documentation/Wiki/Glossary.md is 'not yet written' but it exists
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-03
completed:
---
# BL-1383 — Fix AF-0040: CLAUDE.md says Documentation/Wiki/Glossary.md is 'not yet written' but it exists

## Goal

The defect the audit office reported as AF-0040 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0040 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0040-claude-md-says-documentation-wiki-glossary-md-is-n.md`.

Location: `CLAUDE.md:228`

Location: `CLAUDE.md:228`

CLAUDE.md line 228: 'Documentation/Wiki/Glossary.md (not yet written; align-and-document starts it on its first run)'. The file exists, begins '# Glossary' and has a term table with a 'Name in code' column.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern 'not yet written'; Test-Path Documentation/Wiki/Glossary.md
```

- Expected: No 'not yet written' claim while the file exists.
- Actual: CLAUDE.md:228 matches 'not yet written' and Test-Path returns True.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
