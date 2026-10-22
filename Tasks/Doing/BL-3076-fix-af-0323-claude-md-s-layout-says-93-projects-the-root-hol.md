---
id: BL-3076
title: Fix AF-0323: CLAUDE.md's layout says 93 projects; the root holds 98 C# projects (103 in Curl.slnx with the shared projects)
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-22
completed:
---
# BL-3076 — Fix AF-0323: CLAUDE.md's layout says 93 projects; the root holds 98 C# projects (103 in Curl.slnx with the shared projects)

## Goal

The defect the audit office reported as AF-0323 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0323 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0323-claude-md-s-layout-says-93-projects-the-root-holds.md`.

Location: `CLAUDE.md:297`

Location: `CLAUDE.md:297`

CLAUDE.md:297: '├── ...                           ← 93 projects, one flat alphabetical run'. 98 directories directly under the root hold a same-named .csproj, Curl.slnx lists 98 .csproj paths, and Curl.slnx has 103 <Project> entries counting the five .shproj files.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern '\d+ projects, one flat'; (Get-ChildItem -Directory | Where-Object { Test-Path (Join-Path $_.FullName ($_.Name + '.csproj')) }).Count
```

- Expected: The count in CLAUDE.md equals the number printed.
- Actual: CLAUDE.md:297 says 93 projects; the command prints 98.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
