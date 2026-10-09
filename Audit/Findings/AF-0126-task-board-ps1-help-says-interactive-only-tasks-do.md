---
id: AF-0126
title: task-board.ps1 help says interactive-only tasks do not count toward capacity, but every task in Doing is counted
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:.claude/skills/task-board/task-board.ps1:capacity:false-help
reproduction: none
task: BL-1876
tasks: BL-1876
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0126 - task-board.ps1 help says interactive-only tasks do not count toward capacity, but every task in Doing is counted

## Summary

Low finding from the truthfulness auditor at `.claude/skills/task-board/task-board.ps1:32`: task-board.ps1 help says interactive-only tasks do not count toward capacity, but every task in Doing is counted. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `.claude/skills/task-board/task-board.ps1:32`

Help, 'capacity' command (lines 27-32): 'How many tasks the board could have running at once right now: the tasks in Doing, plus the ready tasks that could start beside them ... The dark factory's -Lanes Auto caps its lane count with it. Interactive-only tasks do not count.' Code (lines 467 and 483): '$doing = @($tasks | Where-Object { $_.State -eq ''Doing'' })' and 'Capacity {0}' = $doing.Count + $picked.Count. Only the ready tasks added on top (Get-LaneReadyTasks) leave out interactive-only ones; an interactive-only task in Doing (lane: no, or touches an audit path) is counted. So the capacity that -Lanes Auto caps its lanes with goes up by one for each interactive session's task in Doing, which the help says cannot happen.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -SimpleMatch 'Interactive-only tasks do not count','$doing = @($tasks | Where-Object { $_.State -eq ''Doing'' })'
```

- Expected: Either the help says interactive-only tasks already in Doing are counted, or the $doing count leaves out tasks whose LaneAllowed is false.
- Actual: task-board.ps1:32 '... Interactive-only tasks do not count.' and task-board.ps1:467 '$doing = @($tasks | Where-Object { $_.State -eq 'Doing' })' with no LaneAllowed filter, summed into 'Capacity {0}' at line 483.

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Ran the reproduction, and both lines matched. task-board.ps1:32 still says 'Interactive-only tasks do not count.', and task-board.ps1:468 still sets '$doing = @($tasks | Where-Object { $_.State -eq 'Doing' })'. The Capacity line adds $doing.Count, so every task in Doing is counted, interactive-only ones included. Only the ready tasks are filtered, through Get-LaneReadyTasks.
- 2026-10-09 | 2026-10-09_0647.md | reproduces: no | Ran the reproduction: 'Interactive-only tasks do not count' no longer matches; only line 474 '$doing = @($tasks | Where-Object { $_.State -eq 'Doing' })' matches. The capacity help (lines 28-33) now says 'Every task in Doing counts, interactive-only ones included; of the ready tasks, only lane-eligible ones do', which matches the code.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
