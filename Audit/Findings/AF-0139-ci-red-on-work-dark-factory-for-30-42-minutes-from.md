---
id: AF-0139
title: CI red on work/dark-factory for 30.42 minutes from run 37931431648 on ea846a2e: lane task BL-1876 edited the guard file .claude/skills/task-board/task-board.ps1
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-09_1435.md: the reproduction no longer reproduces.
key: process:logs:37931431648:ci-red
reproduction: none
task: BL-1891
tasks: BL-1891
found: 2026-10-09
found-at: 71f3acef7ec0d988d2d6b5d967a7b7300156cf44
scorecard: 2026-10-09_0647.md
duplicate-of:
closed: 2026-10-09
closed-how: reliable-reaudit
closed-by: 2026-10-09_1435.md
---
# AF-0139 - CI red on work/dark-factory for 30.42 minutes from run 37931431648 on ea846a2e: lane task BL-1876 edited the guard file .claude/skills/task-board/task-board.ps1

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red on work/dark-factory for 30.42 minutes from run 37931431648 on ea846a2e: lane task BL-1876 edited the guard file .claude/skills/task-board/task-board.ps1.

## Evidence

Location: `logs/ci-runs.json`

Measure-FactoryProcess.ps1 -Since 2026-10-09: ciRedMinutes 41.94, ciRedSpells includes start 2026-10-09T12:45:17Z, end 2026-10-09T13:15:42Z, 30.42 minutes, runId 37931431648, which is over the 30-minute bar for one spell. Cause: BL-1876 (a docs task with touches: [.claude], claimed on lane 1 of shift 20261009-050349 at 05:18:59 local, logs/DarkFactory-20261009-050349-L1.log:23-32) committed e34a8d5a8 'docs(task-board): say every task in Doing counts toward capacity'. That commit changed .claude/skills/task-board/task-board.ps1, one of the four audit guard files lanes may not change. task-board.ps1's claim guard did not refuse the task because its touches named the broad folder .claude, not the guard file. The audit-guard CI job failed on run 37931431648 (ea846a2e, created 12:39:05Z, finished 12:45:17Z) and again on 37933508564 (c0b0a8cc). Shift-end merges were refused: 'not merged: CI failure on ea846a2' (logs/DarkFactory-20261009-050349.log:52, 05:45:40) and 'not merged: CI failure on c0b0a8c' (logs/DarkFactory-20261009-054609.log:16), followed by the 4-stage alarm. Time to file: the coordinator filed BL-1882 at 05:46:02 local, 45 s after the red (logs/DarkFactory-20261009-050349.log:53-54). BL-1882 was interactive only, so lanes could not take it. Time to fix: an interactive session pushed bfa5bbc01 (06:08:27, which deleted all 659 lines of task-board.ps1) and 45d1dc049 (06:08:37, which restored master's copy). Run 37934762067 on 45d1dc04 went green at 13:15:42Z. Commit f38a77c90 (06:22) then hardened guard-audit-paths.ps1 and task-board.ps1 against lanes changing guard files and against broad touches. No merge to master happened while CI was red: master was merged at 71f3ace only after run 37937654435 passed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json | Out-Null; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells; git show --stat e34a8d5a8; Select-String -Path ..\logs\DarkFactory-20261009-050349.log,..\logs\DarkFactory-20261009-054609.log -Pattern '37931431648|not merged|BL-1882'
```

- Expected: No CI red spell over 30 minutes, and no lane commit touching a guard file.
- Actual: Spell 2026-10-09T12:45:17Z to 13:15:42Z, 30.42 minutes, runId 37931431648. e34a8d5a8 changes .claude/skills/task-board/task-board.ps1. Two 'not merged: CI failure' lines and 'BL-1882 ci filed: regression audit guard .claude/skills/task-board/task-board.ps1'.

## Re-audits

- 2026-10-09 | 2026-10-09_1435.md | reproduces: yes | still reported
- 2026-10-09 | 2026-10-09_1435.md | reproduces: no | Ran the reproduction. ciRedSpells still lists 37931431648 (30.42 minutes) because -Since 2026-10-09 includes the original incident. git show --stat e34a8d5a8 still shows BL-1876 changing .claude/skills/task-board/task-board.ps1. The Select-String step could not run: DarkFactory-20261009-050349.log and DarkFactory-20261009-054609.log are not in this log copy. Re-audited by mechanism (a lane editing a guard file and turning CI red) over this window: git log 71f3acef..4653e86a over the guard paths shows only 5b389bddb (BL-1888). That commit was made by hand through the audit branch (task log: 'Interactive', 'Fixed through the audit branch, PR #94'), not by a lane. No CI run on work/dark-factory failed after the 09:39 shift, which finished 7 tasks. f38a77c90 makes the hook refuse a lane's edit of any guard file. No new case of this cause.

## Log

- 2026-10-09: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_1435.md: the reproduction no longer reproduces.
