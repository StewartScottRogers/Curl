---
id: BL-4430
title: Start the task board with pwsh off Windows in Invoke-Board
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskIds.ps1]
requirement: none
created: 2026-10-31
completed:
---
# BL-4430 — Start the task board with pwsh off Windows in Invoke-Board

## Goal

A shift on Linux or macOS can call the task board: `Invoke-Board` starts it with `Get-FactoryPowerShellExe`, as every other board call in `DarkFactory/` does.

## Context

`DarkFactory/TaskIds.ps1:9` ran `& powershell`, which does not exist off Windows, so every board call through `Invoke-Board` (claim, move, next) failed there. The `dark-factory-unix-shift` workflow's one-task shift has failed on every run in its last 200 (e.g. run 41241964612 on 555dc7e2b): its lane could not claim, so the stand-in task never reached Done. The platform seam is `DarkFactory/Platform.ps1` (BL-3919).

## Acceptance criteria

- [ ] `Invoke-Board` in `DarkFactory/TaskIds.ps1` starts the board with `Get-FactoryPowerShellExe`.
- [ ] No file under `DarkFactory/` starts `powershell` by name for a child script other than through `Get-FactoryPowerShellExe` (Restart.ps1's Windows-only sleeper excepted).
- [ ] A detached lane off Windows gets its whole `sh -c` script: `Get-DetachedStartCommand` quotes it, since `Start-Process` joins `-ArgumentList` and .NET splits it again at spaces (`sh` was given only `exec` and the lane exited with no log).
- [ ] `RunDarkFactory.ps1 -TestTaskIds` and `-TestLaneMarker` passes under Windows PowerShell and pwsh.
- [ ] The `dark-factory-unix-shift` workflow's one-task shift passes on Linux and macOS for the fix commit.

## Notes

## Log

- 2026-10-31: Created.
