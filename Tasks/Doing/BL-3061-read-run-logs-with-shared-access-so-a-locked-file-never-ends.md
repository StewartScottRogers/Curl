---
id: BL-3061
title: Read run logs with shared access so a locked file never ends a lane
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskBudget.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3061 — Read run logs with shared access so a locked file never ends a lane

## Goal

Reading a run log that another process still holds open for writing succeeds, so a reader never ends a lane with a script error.

## Context

At 01:51:35 Arizona on 2026-10-22, lane 3 of shift 20261022-014530 ended "script error": `Exception calling "ReadLines" with "1" argument(s): "The process cannot access the file ..."` (Z:\repos\Curl.logs\DarkFactory-20261022-014530-L3.log line 8; the file name is cut off in the log). The only `ReadLines` in `DarkFactory/` is in `Get-LogEstimatedCostUsd` in `DarkFactory/TaskBudget.ps1` (line 57), which reads a run's `.jsonl` log that a running claude may still be writing (`Get-TaskSpentUsd` and `Get-RunLogCost` call it for every log of a task in the last day). `File.ReadLines` opens with `FileShare.Read`, which fails against a writer.

Fix: read the lines through a `FileStream` opened with `FileMode.Open, FileAccess.Read, FileShare.ReadWrite -bor FileShare.Delete` and a `StreamReader`, and retry briefly (for example 5 times, 200 ms apart) on `IOException` before giving up. If it still fails, treat the log as unreadable (cost not counted) and trace it; never throw. Check the other readers of files that other processes write in `DarkFactory/` that use `Get-Content` (for example `Get-RunLogCost` and `Close-UnfinishedRunLogs` in TaskBudget.ps1, which already use `-ErrorAction SilentlyContinue`) and leave them alone unless they can still throw a terminating error; name any such other file in Notes as a follow-up instead of widening this task. The existing self-test is `RunDarkFactory.ps1 -TestTaskBudget` (body in `DarkFactory/TaskBudget.ps1`, line 163 area). Do not edit `.claude/skills/task-board/task-board.ps1` or other guard files.

## Acceptance criteria

- [ ] `-TestTaskBudget` has a case that holds a `.jsonl` open for writing with `FileShare.Read` (or none) from another handle, with assistant usage lines already written, and `Get-LogEstimatedCostUsd` returns the same cost it returns once the handle is closed.
- [ ] `-TestTaskBudget` has a case where the file stays locked exclusively for the whole call: the function returns 0.0 without throwing.
- [ ] `Get-LogEstimatedCostUsd` no longer calls `[System.IO.File]::ReadLines`, and `Select-String 'ReadLines' DarkFactory\*.ps1` finds none.
- [ ] `RunDarkFactory.ps1 -TestTaskBudget` passes and `RunDarkFactory.ps1` still parses.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 21dfe1e6 37c11953 5be53930 2448417f.
