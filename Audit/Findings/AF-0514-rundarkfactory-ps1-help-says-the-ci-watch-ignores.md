---
id: AF-0514
title: RunDarkFactory.ps1 help says the CI watch ignores cancelled runs, but it now reads them for hangs and files Fix CI hang tasks
auditor: truthfulness
severity: Medium
status: proposed
reason:
key: truthfulness:RunDarkFactory.ps1:CI-WATCH-help:false-help
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: 23bacbc6c04b8e06a4eecb79636da4891dca4c01
scorecard: 2026-10-31_1149.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0514 - RunDarkFactory.ps1 help says the CI watch ignores cancelled runs, but it now reads them for hangs and files Fix CI hang tasks

## Summary

Medium finding from the truthfulness auditor at `RunDarkFactory.ps1:222`: RunDarkFactory.ps1 help says the CI watch ignores cancelled runs, but it now reads them for hangs and files Fix CI hang tasks. Reported by an auditor flagged unreliable in 2026-10-31_1149.md.

## Evidence

Location: `RunDarkFactory.ps1:222`

The CI WATCH section of the comment-based help (lines 221-223) says the watch 'reads the newest finished CI runs on the shift's branch that passed or failed - cancelled ones prove nothing, and with several lanes pushing most are cancelled'. Its list of filed task titles has no hang task. The code does something else. DarkFactory/CiWatch.ps1:502-504 (Get-CiRuns) takes 'a cancelled run only when a job ran into its timeout-minutes (Get-CiHangs, read through -ReadJobs), whose hangs are its failures'. Get-CiHangs (line 385) reads each cancelled job that ran at least $CiHangSilenceMinutes (15). Get-CiHang (line 350) keys it 'hang in <job> (<assembly>)', and the self-test at line 1228 shows it filed as a High 'Fix CI hang in ...' task. An agent reading the help would think a cancelled run never files a task. Phase 2: explained by ADR-1173 (BL-4420), which decided this change; the help was not updated with it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path RunDarkFactory.ps1, DarkFactory/CiWatch.ps1 -Pattern 'cancelled ones prove nothing', 'a cancelled run only when a job ran into its timeout-minutes' | ForEach-Object { $_.Filename + ':' + $_.LineNumber }
```

- Expected: Only CiWatch.ps1:503 matches: the help describes cancelled runs being read for hangs.
- Actual: RunDarkFactory.ps1:222 and CiWatch.ps1:503 both match: the help says cancelled runs prove nothing while the code reads them for hangs.

## Re-audits

## Log

- 2026-10-31: filed proposed.
