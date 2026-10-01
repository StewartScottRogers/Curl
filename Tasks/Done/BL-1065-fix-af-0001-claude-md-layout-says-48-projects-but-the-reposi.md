---
id: BL-1065
title: Fix AF-0001: CLAUDE.md layout says '48 projects' but the repository has 66
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1065 — Fix AF-0001: CLAUDE.md layout says '48 projects' but the repository has 66

## Goal

The defect the audit office reported as AF-0001 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0001 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0001-claude-md-layout-says-48-projects-but-the-reposito.md`.

Location: `CLAUDE.md`

Location: `CLAUDE.md`

The Repository layout tree in CLAUDE.md says '...  <- 48 projects, one flat alphabetical run'. The repository root holds 66 Curl.* project directories (32 UnitLibrary, 32 UnitTests, Curl.Console, Curl.Console.UnitTests), each with a csproj, and Curl.slnx lists them all flat.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path CLAUDE.md -Pattern '\d+ projects, one flat').Line; (Get-ChildItem -Directory -Filter 'Curl.*').Count
```

- Expected: The count in CLAUDE.md equals the number of Curl.* project directories.
- Actual: CLAUDE.md says 48 projects; Get-ChildItem returns 66 directories.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- CLAUDE.md line 198 now says "66 projects, one flat alphabetical run". That is the 66 Curl.* project directories at the root, each with a csproj. The four shared projects (Documentation, Tasks, Audit, .claude) are not counted: they appear in the tree by name.
- Choice: I kept a number rather than removing it. The finding's reproduction matches the pattern `d+ projects, one flat`, so the wording keeps that exact shape. The number will drift as hand-built libraries are added, and the truthfulness auditor will catch that.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. CLAUDE.md's layout now gives the true project count (66), so AF-0001's reproduction matches
