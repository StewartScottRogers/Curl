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
completed: 2026-10-31
---
# BL-4430 — Start the task board with pwsh off Windows in Invoke-Board

## Goal

A shift on Linux or macOS can call the task board: `Invoke-Board` starts it with `Get-FactoryPowerShellExe`, as every other board call in `DarkFactory/` does.

## Context

`DarkFactory/TaskIds.ps1:9` ran `& powershell`, which does not exist off Windows, so every board call through `Invoke-Board` (claim, move, next) failed there. The `dark-factory-unix-shift` workflow's one-task shift has failed on every run in its last 200 (e.g. run 41241964612 on 555dc7e2b): its lane could not claim, so the stand-in task never reached Done. The platform seam is `DarkFactory/Platform.ps1` (BL-3919).

## Acceptance criteria

- [x] `Invoke-Board` in `DarkFactory/TaskIds.ps1` starts the board with `Get-FactoryPowerShellExe`.
- [x] No file under `DarkFactory/` starts `powershell` by name for a child script other than through `Get-FactoryPowerShellExe` (Restart.ps1's Windows-only sleeper excepted).
- [x] A detached lane off Windows gets its whole `sh -c` script: `Get-DetachedStartCommand` quotes it, since `Start-Process` joins `-ArgumentList` and .NET splits it again at spaces (`sh` was given only `exec` and the lane exited with no log).
- [x] `RunDarkFactory.ps1 -TestTaskIds` and `-TestLaneMarker` passes under Windows PowerShell and pwsh.
- [x] The `dark-factory-unix-shift` workflow's one-task shift passes on Linux and macOS for the fix commit.

## Notes

- 2026-10-31 (interactive): run 41244030532 of dark-factory-unix-shift on the two fix commits: 4 of 4 PASS, the lane claimed BL-4430's stand-in, pushed its change and moved it to Done. The workflow runs on ubuntu-latest only, so macOS rests on the same code path (sh, pwsh, Start-Process). Runs 41241964612 and 41243635885 failed before each fix.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: adc685eb 3c83af8c 72c73e7a 782f81de 1eec992f.
- 2026-10-31: Doing -> Done. Invoke-Board uses Get-FactoryPowerShellExe and a detached lane's sh -c script is quoted; dark-factory-unix-shift run 41244030532 passed 4 of 4
