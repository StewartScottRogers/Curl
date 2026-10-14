---
id: BL-2214
title: Fix AF-0212: Root CLAUDE.md's layout says 73 projects; Curl.slnx lists 75 C# projects
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-14
completed:
---
# BL-2214 — Fix AF-0212: Root CLAUDE.md's layout says 73 projects; Curl.slnx lists 75 C# projects

## Goal

The defect the audit office reported as AF-0212 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0212 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0212-root-claude-md-s-layout-says-73-projects-curl-slnx.md`.

Location: `CLAUDE.md:262`

Location: `CLAUDE.md:262`

CLAUDE.md:262 reads '├── ...  ← 73 projects, one flat alphabetical run'. Curl.slnx lists 75 .csproj projects (plus 5 shared-project entries), and 75 Curl.*/*.csproj exist. No reading gives 73: not the total (75), and not the total less the five shown (70).

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.slnx -Pattern '\.csproj"').Count; (Select-String -Path CLAUDE.md -Pattern '73 projects').Count
```

- Expected: The project count in CLAUDE.md matches the solution: 75, and no '73 projects' line.
- Actual: 75 / 1

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
