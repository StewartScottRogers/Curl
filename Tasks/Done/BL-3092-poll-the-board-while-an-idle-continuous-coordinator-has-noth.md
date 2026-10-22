---
id: BL-3092
title: Poll the board while an idle -Continuous coordinator has nothing claimable
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3090]
touches: [DarkFactory/ShiftCoordinator.ps1, DarkFactory/Heartbeat.ps1, DarkFactory/LaneState.ps1, RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3092 — Poll the board while an idle -Continuous coordinator has nothing claimable

## Goal

After a `-Continuous` shift ends with nothing claimable, the coordinator keeps polling the board and starts the next shift as soon as anything is claimable, publishing an "idle, polling" heartbeat meanwhile.

## Context

Evidence: the 2026-10-22 02:13 shift ended "nothing ready" (`DarkFactory-20261022-014530.log`). Tasks became ready at about 02:40 (BL-3011 landed and 30 docs tasks were filed) but no shift started until a manual `-Restart` at 02:44.

Where: the shift-end block of `DarkFactory/ShiftCoordinator.ps1` (~lines 440-470): when `$Continuous` is set and `Get-NextTaskId $stillReady` is empty it falls through to the alarm or `Close-OwnHerdrTab` and `exit 0`. Add a polling loop there instead: every `-IdlePollMinutes` (new parameter in `RunDarkFactory.ps1`, `[ValidateRange(1, 60)]`, default 5, documented in the help text and forwarded in the `$forward` hand-over list) pull the shift branch (`git pull -q --ff-only origin $branch`), recompute claimable work with the BL-3090 predicate, and when something is claimable start the next shift exactly as the existing hand-over does (`Start-Detached`, same forwarded arguments). Stop polling at the `-Hours` deadline or when the usage limits (`-StopAtUsage`, `-StopAtWeeklyUsage`, `DarkFactory/Usage.ps1`) say a shift must not start; keep the existing alarm for the `$reasons` list (waiting-on-Stewart items) shown once before polling, and keep the review (ADR-0710) behaviour. While polling, write a heartbeat with phase 'idle' and step 'polling' (`Write-Heartbeat` / `Publish-BoardStatus` in `DarkFactory/Heartbeat.ps1`; the `-TestHeartbeat` self-test is in `DarkFactory/LaneState.ps1`) so the board page shows it. The loop body must be a pure decision function with injected board, clock and usage inputs so a self-test can drive it.

Depends on BL-3090 for the claimable-now predicate (it also makes this file's overlap with that task a data dependency).

## Acceptance criteria

- [x] `RunDarkFactory.ps1` has `-IdlePollMinutes` (default 5, range 1-60) with help text, and the `-Continuous` hand-over forwards it.
- [x] A pure poll-decision function exists in `DarkFactory/ShiftCoordinator.ps1` and `RunDarkFactory.ps1 -TestHeartbeat` (extended) passes with cases: nothing claimable and time left gives "wait"; something claimable gives "start next shift"; past the `-Hours` deadline gives "stop"; usage over the stop threshold gives "stop"; only cost-capped tasks gives "wait".
- [x] The same self-test shows the heartbeat written while polling has phase 'idle' and step 'polling'.
- [x] The coordinator's idle loop calls that function every `-IdlePollMinutes` and uses the existing `Start-Detached` hand-over (code review: no second copy of the argument list).
- [x] A non-Continuous shift's end is unchanged (still alarm or close, no polling).
- [x] All `RunDarkFactory.ps1 -Test*` switches that passed before still pass.

## Notes

- Pipeline feature, done directly: the change is PowerShell in four DarkFactory files and has no C# seams for protocol-architect to plan.
- `Get-IdlePollDecision` (pure: now, deadline, usage-stop reason, claimable ID, capped IDs) and `Write-IdlePollHeartbeat` are defined at the top of `DarkFactory/ShiftCoordinator.ps1`, outside the coordinator block. `-TestHeartbeat` lives in `LaneState.ps1`, which loads before the coordinator file, so the self-test dot-sources `ShiftCoordinator.ps1` with `$Lane` set (its coordinator block is skipped) and then resets `$AreaFileFinished`. Without that reset the loader carried on past the test's exit, into ShiftRunner.
- The hand-over is one scriptblock, `$startNextShift`, used by both the work-still-ready path and the poll. `-IdlePollMinutes` is in its argument list.
- Decision (ADR-0758): the waiting-on-Stewart reasons are traced once before polling and the tab label says "ALARM, read; idle, polling". `Invoke-Alarm` waits for a key press, so it rings when polling stops, not before; ringing it first would hold the poll all night.
- Decision: the coordinator writes the idle heartbeat as lane 0 (`$Lane` is 0 in the coordinator) and refreshes it every minute of the wait. Polling stops at this shift's own `-Hours` deadline (`$shiftEnd`).
- Verified: `-TestHeartbeat` passes its 6 new cases. All 20 non-interactive `-Test*` switches pass (exit 0, no FAIL). `-TestAlarm`, `-TestMachineProbe` and `-TestOutOfTokens` were not run: they sound the alarm, probe the machine or wait, and they exit in files loaded before the changed code. `dotnet build` is clean and the fast tests pass (0 failed).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 607229d4 5b3f2fcd 30be321b 956f59f8 fd9eac29 7193fd91.
- 2026-10-22: Doing -> Done. An idle -Continuous shift polls the board every -IdlePollMinutes and starts the next shift once a task is claimable
