---
id: BL-2257
title: Fix AF-0220: Lanes idled 652 of 971.62 lane minutes (67%) behind BL-2235, a High gap task filed with touches: [] (read as *) that overlapped BL-2160 left in Doing by an interactive session
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/task-board.ps1]
requirement: none
created: 2026-10-14
completed:
---
# BL-2257 — Fix AF-0220: Lanes idled 652 of 971.62 lane minutes (67%) behind BL-2235, a High gap task filed with touches: [] (read as *) that overlapped BL-2160 left in Doing by an interactive session

## Goal

The defect the audit office reported as AF-0220 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0220 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0220-lanes-idled-652-of-971-62-lane-minutes-67-behind-b.md`.

Location: `logs/DarkFactory-20261014-090135-L1.log:138`

Location: `logs/DarkFactory-20261014-090135-L1.log:138`

Measure-FactoryProcess.ps1 -Since 2026-10-14: laneIdleMinutes 652, waitOverlapMinutes 652, waitNothingReadyMinutes 0, laneMinutes 971.62 (67%, over the 20% threshold). Every wait line reads 'No task can start yet: every ready task overlaps one in Doing or waits behind one that does'. Lane 1 waited 09:02-09:07 and 10:22:06-15:38:58 (DarkFactory-20261014-090135-L1.log lines 2-6 and 138 onward). Lane 2 waited 09:53-15:35 and was retired with done=2. Lane 3 waited 10:02-10:34 and was retired with done=0. The coordinator held 2 lanes ('lanes 2 held (no burn rate yet)') from 10:48 to 15:20. Board state at 496f70e6f (09:50:42, 'chore(tasks): file gap tasks from 2026-10-14_0904'): Doing held BL-2160 (touches [Gap]; claimed interactively at e3300df8, 08:53; depends on 15 tasks) and BL-2218 (touches [Audit, .claude/agents], interactive). BL-2235 was filed that minute with 'priority: High' and 'touches: []'. task-board.ps1 ConvertTo-Task (line 170) turns an empty touches into '*', and Test-Overlap (line 243) makes '*' overlap everything. So BL-2235 collided with BL-2160 in Doing, and Get-StartOrder (lines 349-361) let it reserve every touch, which held back each ready Curl.Conformance task (BL-2221/2222/2223, touches [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]) and BL-2215/2216 ([RunDarkFactory.ps1]), none of which overlapped anything in Doing. Commit fe98450af (15:38:36) deferred BL-2160 and BL-2235, and lane 1 claimed BL-2221 at 15:38:56, 20 s later. The same cause was recorded that morning in e53f34102 (09:07, 'BL-2160 touches Gap only (it held every lane back)'). A true, narrower touches for BL-2235 (the --httpsig-* options' CLI projects) would not have serialised the board. Gap/Tools/New-TasksFromGaps.ps1 line 28 files such a task with no touches by design ('runs alone'). An orphaned interactive claim in Doing turned 'runs alone' into 'nothing runs'.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-14 -LogRoot Z:\repos\Curl.audit\20261014-181528\logs -CiRunsJson Z:\repos\Curl.audit\20261014-181528\logs\ci-runs.json -OutFile $env:TEMP\process.json; $p = Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json; '{0} of {1} lane minutes' -f $p.waitOverlapMinutes, $p.laneMinutes; git show 496f70e6f:Tasks/Backlog/BL-2235-re-close-gf-0040-upstream-curl-8-22-0-changed-options-items.md | Select-String '^(priority|touches):'; git log -1 --format='%ci %s' fe98450af
```

- Expected: waitOverlapMinutes at most 20% of laneMinutes, and no lane-eligible task with empty touches waiting in the queue while an interactive task sits in Doing.
- Actual: 652 of 971.62 lane minutes; priority: High; touches: []; 2026-10-14 15:38:36 -0700 chore(tasks): defer BL-2160 and BL-2235 to the next gap run

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-14 (Claude): The history the reproduction measures cannot change, so fix the cause instead. The cause is in task-board.ps1's Get-StartOrder: a ready task with touches '*' that cannot start (it overlaps a task in Doing) still reserves every touch and holds back ready tasks that overlap nothing in Doing. Make a waiting '*' task reserve nothing it cannot claim, with a self-test for this exact board (an interactive task in Doing, a '*' task and a narrow task in Backlog: the narrow task starts). task-board.ps1 is a guard file, so this is interactive work on the audit branch; touches retargeted from RunDarkFactory.ps1.

## Log

- 2026-10-14: Created.
- 2026-10-15: Backlog -> Doing. Interactive: task-board.ps1 is a guard file.
