---
id: BL-2562
title: Re-fix AF-0224: Root CLAUDE.md's repository layout says 75 projects; Curl.slnx lists 79
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2562 — Re-fix AF-0224: Root CLAUDE.md's repository layout says 75 projects; Curl.slnx lists 79

## Goal

The defect the audit office reported as AF-0224 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0224 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0224-root-claude-md-s-repository-layout-says-75-project.md`.

Re-fix: the earlier task(s) BL-2277 reached Done, and a later re-audit by the truthfulness auditor found the reproduction still reproduces:

- 2026-10-16 | 2026-10-16_1623.md | reproduces: yes | Ran the reproduction: 93, then '... 84 projects, one flat alphabetical run'. The count in CLAUDE.md changed (from 75 to 84), but it still does not match the 93 .csproj entries in Curl.slnx. 93 csproj files are also on disk (ls */*.csproj).

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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

CLAUDE.md layout count set to 93, the current Curl.slnx .csproj count (docs only; fast tests unaffected, build clean).

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-17: Doing -> Done. CLAUDE.md count now 93, matching Curl.slnx
