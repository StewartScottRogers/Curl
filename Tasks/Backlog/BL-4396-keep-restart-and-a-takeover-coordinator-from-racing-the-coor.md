---
id: BL-4396
title: Keep -Restart and a takeover coordinator from racing the coordinator watchdog
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Restart.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-31
completed:
---
# BL-4396 — Keep -Restart and a takeover coordinator from racing the coordinator watchdog

## Goal

-Restart and a -TakeOverShift coordinator never trigger the coordinator watchdog, so a shift never ends up with two coordinators or a replacement killed every 10 minutes.

## Context

Shift-end review 2026-10-31 (factory code-reviewer) of BL-4353 / ADR-1160. DarkFactory/Restart.ps1:104 kills the coordinator and then waits for lanes in claim or integrate; the dead coordinator's coordinator.stamp stays, lanes keep running Invoke-CoordinatorWatchdogIfDue (Heartbeat.ps1:380-412), and after 9 minutes one starts a -TakeOverShift coordinator - then -Restart starts the next shift too: two coordinators on one branch. Heartbeat.ps1:391 also walks the children of a dead coordinator pid, which Windows may have reused. A takeover coordinator writes its first stamp only at the loop top (ShiftCoordinator.ps1:534), after Wait-ForFreshSession (275) and Get-MachineCap -Probe (251), which can block for hours, so once the 10-minute coordinator.replacing marker expires lanes kill and replace it again and again.

## Acceptance criteria

- [ ] -Restart removes coordinator.stamp or creates coordinator.replacing before stopping the coordinator, shown by a self-test.
- [ ] Restart-StaleCoordinator kills child processes only of a coordinator pid that is alive, shown by a self-test.
- [ ] A -TakeOverShift coordinator writes its stamp before any start-up step that can block (or skips the fresh-session wait and the probe), shown by a self-test.
- [ ] The factory's -Test* self-tests pass under Windows PowerShell and pwsh.

## Notes

## Log

- 2026-10-31: Created.
