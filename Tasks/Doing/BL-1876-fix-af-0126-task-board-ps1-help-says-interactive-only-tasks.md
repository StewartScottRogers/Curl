---
id: BL-1876
title: Fix AF-0126: task-board.ps1 help says interactive-only tasks do not count toward capacity, but every task in Doing is counted
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [.claude]
requirement: none
created: 2026-10-09
completed:
---
# BL-1876 — Fix AF-0126: task-board.ps1 help says interactive-only tasks do not count toward capacity, but every task in Doing is counted

## Goal

The defect the audit office reported as AF-0126 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0126 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0126-task-board-ps1-help-says-interactive-only-tasks-do.md`.

Location: `.claude/skills/task-board/task-board.ps1:32`

Location: `.claude/skills/task-board/task-board.ps1:32`

Help, 'capacity' command (lines 27-32): 'How many tasks the board could have running at once right now: the tasks in Doing, plus the ready tasks that could start beside them ... The dark factory's -Lanes Auto caps its lane count with it. Interactive-only tasks do not count.' Code (lines 467 and 483): '$doing = @($tasks | Where-Object { $_.State -eq ''Doing'' })' and 'Capacity {0}' = $doing.Count + $picked.Count. Only the ready tasks added on top (Get-LaneReadyTasks) leave out interactive-only ones; an interactive-only task in Doing (lane: no, or touches an audit path) is counted. So the capacity that -Lanes Auto caps its lanes with goes up by one for each interactive session's task in Doing, which the help says cannot happen.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -SimpleMatch 'Interactive-only tasks do not count','$doing = @($tasks | Where-Object { $_.State -eq ''Doing'' })'
```

- Expected: Either the help says interactive-only tasks already in Doing are counted, or the $doing count leaves out tasks whose LaneAllowed is false.
- Actual: task-board.ps1:32 '... Interactive-only tasks do not count.' and task-board.ps1:467 '$doing = @($tasks | Where-Object { $_.State -eq 'Doing' })' with no LaneAllowed filter, summed into 'Capacity {0}' at line 483.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Decided: fix the help, not the code. Tasks already in Doing are real running work, so Capacity counts them; the help now says so.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
