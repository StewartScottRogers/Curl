---
id: BL-3116
title: Stop Invoke-TaskSplit's Board parameter shadowing the board script path
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskStash.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3116 — Stop Invoke-TaskSplit's Board parameter shadowing the board script path

## Goal

Invoke-TaskSplit (DarkFactory/TaskStash.ps1) has a `[scriptblock]$Board` parameter; Invoke-Board (TaskIds.ps1) reads the script-path variable `` by dynamic scope, so inside Invoke-TaskSplit the default board call runs a scriptblock as a script file and the move to Blocked never takes. Rename the parameter (found by BL-3115, whose helper had the same trap) and add a case that runs the default board.

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] The parameter no longer shadows the board script path, and a self-test shows the default board call moves a task to Blocked.
- [x] `RunDarkFactory.ps1 -TestTaskStash` passes.
- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- Renamed Invoke-TaskSplit's `-Board` to `-BoardCall` (the old name shadowed the `$Board` script path Invoke-Board reads by dynamic scope); added a self-test running the default board with `$Board` pointed at a recording script. The third criterion is an empty template placeholder; ticked as vacuous.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: bc876eef 95ad9451 fbecace0.
- 2026-10-22: Doing -> Done. Invoke-TaskSplit's parameter renamed BoardCall; default board call moves to Blocked, self-test added
