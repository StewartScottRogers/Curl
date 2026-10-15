---
id: BL-2277
title: Fix AF-0224: Root CLAUDE.md's repository layout says 75 projects; Curl.slnx lists 79
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-15
completed:
---
# BL-2277 — Fix AF-0224: Root CLAUDE.md's repository layout says 75 projects; Curl.slnx lists 79

## Goal

The defect the audit office reported as AF-0224 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0224 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0224-root-claude-md-s-repository-layout-says-75-project.md`.

Location: `CLAUDE.md:265`

Location: `CLAUDE.md:265`

The layout block in the root CLAUDE.md reads '...  <- 75 projects, one flat alphabetical run'. Curl.slnx lists 79 .csproj entries, every one a Curl.* directory at the root, and 79 Curl.* directories exist. AF-0212's literal '73 projects' is gone, but the corrected count is still wrong.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.slnx -Pattern '\.csproj"').Count; (Select-String -Path CLAUDE.md -Pattern '\d+ projects, one flat').Line.Trim()
```

- Expected: 79, and the CLAUDE.md line names 79 projects
- Actual: 79, and the CLAUDE.md line reads '... 75 projects, one flat alphabetical run'

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-15: Created.
