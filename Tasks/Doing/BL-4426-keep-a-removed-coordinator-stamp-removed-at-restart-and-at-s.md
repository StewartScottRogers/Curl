---
id: BL-4426
title: Keep a removed coordinator stamp removed at -Restart and at shift end
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Restart.ps1, DarkFactory/Heartbeat.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-31
completed:
---
# BL-4426 — Keep a removed coordinator stamp removed at -Restart and at shift end

## Goal

Once `-Restart` or the shift's end removes `coordinator.stamp`, nothing writes it again, so no lane's watchdog (ADR-1160) replaces a coordinator that was stopped on purpose.

## Context

Found by the shift-end review of 2026-10-31 (ADR-0710; silent-failure-hunter, Important; code-reviewer, Suggestion) in the merge `55fa1ce15...ac0e3e86e` (BL-4396).

- `DarkFactory/Heartbeat.ps1:261-265`: `Invoke-TimedExternalCall` now calls `Write-CoordinatorStamp` before and after every timed `git` and `gh` call.
- `DarkFactory/Restart.ps1:117-118`: `-Restart` calls `Remove-StoppedCoordinatorStamp` while the coordinator is still alive, then `Stop-FactoryProcessTree`. A coordinator in the middle of a timed call rewrites the stamp after the delete, and lanes later replace the "stale" coordinator beside the new shift - what BL-4396 set out to prevent. The delete also hides its failure with `-ErrorAction SilentlyContinue`.
- `DarkFactory/ShiftCoordinator.ps1:658`: the shift's end removes the stamp, then runs `git pull` and the merge's `gh` CI polling through `Invoke-TimedExternalCall`, each of which writes a fresh stamp back.

## Acceptance criteria

- [x] `-Restart` stops the coordinator's process tree before it removes the stamp, and traces a removal that fails.
- [x] After the shift-end removal, `Write-CoordinatorStamp` writes nothing (for example a `$script:CoordinatorStampRetired` flag set at the removal).
- [x] A self-test pins each: after the retire flag is set, a timed external call leaves no stamp file; and `-Restart`'s order is stop, then remove.
- [x] `RunDarkFactory.ps1 -TestRestart` and the heartbeat self-tests pass under Windows PowerShell and `pwsh`.

## Notes

- Restart.ps1: new Stop-CoordinatorThenRemoveStamp (stop, then remove; removal failure traced). Write-CoordinatorStamp returns at once once $script:CoordinatorStampRetired is set at the shift-end removal. Self-tests pass under powershell and pwsh.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: ffcbdbd6 98b7d745 06448ffb 2b72ed81.
