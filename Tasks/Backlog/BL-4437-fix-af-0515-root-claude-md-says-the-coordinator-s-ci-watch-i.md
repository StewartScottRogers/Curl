---
id: BL-4437
title: Fix AF-0515: Root CLAUDE.md says the coordinator's CI watch ignores cancelled runs, but it files hangs from them
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-31
completed:
---
# BL-4437 — Fix AF-0515: Root CLAUDE.md says the coordinator's CI watch ignores cancelled runs, but it files hangs from them

## Goal

The defect the audit office reported as AF-0515 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0515 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0515-root-claude-md-says-the-coordinator-s-ci-watch-ign.md`.

Location: `CLAUDE.md:204`

Location: `CLAUDE.md:204`

The 'Dark factory' section says the coordinator 'reads the finished CI runs on the shift's branch (and the failed jobs of the one still running, ADR-0811) (cancelled ones ignored)'. DarkFactory/CiWatch.ps1:502-504 (Get-CiRuns) includes 'a cancelled run only when a job ran into its timeout-minutes (Get-CiHangs ...), whose hangs are its failures', and those are filed as High 'Fix CI hang in <job> (<assembly>)' tasks (CiWatch.ps1:1228 self-test). An agent reading CLAUDE.md would not expect a cancelled run to file a task. Phase 2: explained by ADR-1173 (BL-4420); CLAUDE.md was not updated with it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md, DarkFactory/CiWatch.ps1 -Pattern '\(cancelled ones ignored\)', 'a cancelled run only when a job ran into its timeout-minutes' | ForEach-Object { $_.Filename + ':' + $_.LineNumber }
```

- Expected: Only CiWatch.ps1:503 matches: CLAUDE.md says a cancelled run whose job timed out is read for hangs.
- Actual: CLAUDE.md:204 and CiWatch.ps1:503 both match.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
