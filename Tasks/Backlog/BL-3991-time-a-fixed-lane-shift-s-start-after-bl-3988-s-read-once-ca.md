---
id: BL-3991
title: Time a fixed-lane shift's start after BL-3988's read-once capacity check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: []
lane: no
requirement: none
created: 2026-10-28
completed:
---
# BL-3991 — Time a fixed-lane shift's start after BL-3988's read-once capacity check

## Goal

The time from launching `RunDarkFactory.cmd -NewTab -Lanes 12` to its `lanes: starting at` line is measured on Stewart's machine and is under 60 s.

## Context

BL-3988 made `Get-BoardCapacity` (`DarkFactory/AutoLanesState.ps1`) read the recent run costs once per capacity check (`Invoke-WithRecentRunCostsReadOnce`, `DarkFactory/TaskBudget.ps1`). A lane measured the cap check on `Z:\repos\Curl.logs` (3,985 run logs) in Windows PowerShell 5.1 while other lanes built: three ready tasks took 88 s reading per task and 30 s reading once. A lane cannot launch a real shift, so the launch itself was not timed.

## Acceptance criteria

- [ ] The next `-Lanes N` shift launch's time to `lanes: starting at` is recorded in this task's Log, and is under 60 s; if not, a task names what else the start spends its time on.

## Notes

Interactive only: timing it means launching a real shift. Remove lane: no once a lane can start and stop a throwaway shift without spending a real one.

## Log

- 2026-10-28: Created by BL-3988's lane run, which could not launch a shift to time it.
