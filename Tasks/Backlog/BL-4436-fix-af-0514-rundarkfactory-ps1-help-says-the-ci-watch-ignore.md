---
id: BL-4436
title: Fix AF-0514: RunDarkFactory.ps1 help says the CI watch ignores cancelled runs, but it now reads them for hangs and files Fix CI hang tasks
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-31
completed:
---
# BL-4436 — Fix AF-0514: RunDarkFactory.ps1 help says the CI watch ignores cancelled runs, but it now reads them for hangs and files Fix CI hang tasks

## Goal

The defect the audit office reported as AF-0514 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0514 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0514-rundarkfactory-ps1-help-says-the-ci-watch-ignores.md`.

Location: `RunDarkFactory.ps1:222`

Location: `RunDarkFactory.ps1:222`

The CI WATCH section of the comment-based help (lines 221-223) says the watch 'reads the newest finished CI runs on the shift's branch that passed or failed - cancelled ones prove nothing, and with several lanes pushing most are cancelled'. Its list of filed task titles has no hang task. The code does something else. DarkFactory/CiWatch.ps1:502-504 (Get-CiRuns) takes 'a cancelled run only when a job ran into its timeout-minutes (Get-CiHangs, read through -ReadJobs), whose hangs are its failures'. Get-CiHangs (line 385) reads each cancelled job that ran at least $CiHangSilenceMinutes (15). Get-CiHang (line 350) keys it 'hang in <job> (<assembly>)', and the self-test at line 1228 shows it filed as a High 'Fix CI hang in ...' task. An agent reading the help would think a cancelled run never files a task. Phase 2: explained by ADR-1173 (BL-4420), which decided this change; the help was not updated with it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path RunDarkFactory.ps1, DarkFactory/CiWatch.ps1 -Pattern 'cancelled ones prove nothing', 'a cancelled run only when a job ran into its timeout-minutes' | ForEach-Object { $_.Filename + ':' + $_.LineNumber }
```

- Expected: Only CiWatch.ps1:503 matches: the help describes cancelled runs being read for hangs.
- Actual: RunDarkFactory.ps1:222 and CiWatch.ps1:503 both match: the help says cancelled runs prove nothing while the code reads them for hangs.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
