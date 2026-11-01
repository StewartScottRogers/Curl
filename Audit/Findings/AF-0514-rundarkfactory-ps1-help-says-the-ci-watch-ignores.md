---
id: AF-0514
title: RunDarkFactory.ps1 help says the CI watch ignores cancelled runs, but it now reads them for hangs and files Fix CI hang tasks
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces.
key: truthfulness:RunDarkFactory.ps1:CI-WATCH-help:false-help
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: 23bacbc6c04b8e06a4eecb79636da4891dca4c01
scorecard: 2026-10-31_1149.md
duplicate-of:
closed: 2026-11-01
closed-how: reliable-reaudit
closed-by: 2026-11-01_0144.md
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

- 2026-11-01 | 2026-11-01_0144.md | reproduces: no | Ran the reproduction: only DarkFactory/CiWatch.ps1:503 matches; RunDarkFactory.ps1 no longer contains 'cancelled ones prove nothing'. Its help (lines 221-225) now says 'a cancelled run is read only when a job ran into its timeout-minutes, whose hang is its failure, filed as a High "Fix CI hang in <job> (<assembly>)" task (ADR-1173)', which matches Get-CiRuns and the hang code in CiWatch.ps1 (lines 352-400).

## Log

- 2026-10-31: filed proposed.
- 2026-11-01: proposed -> closed. Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces.
