---
id: BL-3362
title: Fix AF-0385: CLAUDE.md's repository layout says 98 projects; the tree has 101 project directories with a .csproj plus 5 shared projects
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-23
completed:
---
# BL-3362 — Fix AF-0385: CLAUDE.md's repository layout says 98 projects; the tree has 101 project directories with a .csproj plus 5 shared projects

## Goal

The defect the audit office reported as AF-0385 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0385 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0385-claude-md-s-repository-layout-says-98-projects-the.md`.

Location: `CLAUDE.md:297`

Location: `CLAUDE.md:297`

CLAUDE.md line 297, in the layout diagram: '├── ...  ← 98 projects, one flat alphabetical run'. 101 top-level directories hold a .csproj, and Curl.slnx lists 106 <Project> entries (the 101 plus the shared projects Audit, Documentation, Gap, Tasks and .claude/Claude). The count was set by commit 03ebe7766 (BL-3076) and has not kept up with new projects. It is true in substance (a flat run of many projects) but wrong in its number.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern '98 projects' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }; (Get-ChildItem -Directory | Where-Object { Test-Path (Join-Path $_.FullName '*.csproj') }).Count
```

- Expected: The stated count equals the measured count (or no number is stated).
- Actual: CLAUDE.md:297 then 101

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
