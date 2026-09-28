---
id: BL-768
title: Run the dark factory with -Lanes Auto, scaling lanes one step at a time to the burn rate and ceilings
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-764, BL-765, BL-766, BL-767]
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-768 — Run the dark factory with -Lanes Auto, scaling lanes one step at a time to the burn rate and ceilings

## Goal

`RunDarkFactory.cmd -Lanes Auto` runs a shift that sizes itself. Every 15 minutes the coordinator:
1. samples usage and meters the burn rate per lane;
2. computes the pace target from the 5-hour and weekly windows;
3. caps it by the board's capacity, the machine cap and `-MaxLanes`;
4. adds or retires at most one lane, logging the reason.

It starts from the last shift's lane count and never needs to know the Claude plan. A number (`-Lanes 3`) still runs a fixed shift exactly as before.

## Context

- This is Stewart's design approved 2026-09-28. The ADR from BL-763 is binding throughout. Read it first.
- **Pieces already built:**
  - BL-765's `New-UsageSample`, `Get-BurnRate`, `Get-PaceTarget` and `Get-NextLaneCount`, and its `-TestAutoLanes`;
  - BL-766's `-ProbeMachine` and `machine-lanes.json`;
  - BL-767's `Start-Lane`, `Add-Lane`, `Request-LaneRetire` and the active lane set;
  - BL-764's `task-board.ps1 capacity`, which prints `Capacity <n>: ...`.
- **Parameters:**
  - `-Lanes` becomes `[ValidatePattern('^(?i:auto|[1-9]|1[0-6])$')][string]$Lanes = '1'`. Derive `$AutoLanes` (bool) and an `[int]` lane count right after `param`, and use the count wherever `$Lanes` is used as a number today.
  - Add `[ValidateRange(1, 16)][int]$MaxLanes = 16` and `[switch]$NoWeeklyPace`, documented in `param` like their neighbours.
  - Auto always takes the coordinator path, even at one lane.
- **At shift start, in the coordinator, before `Wait-ForFreshSession`:**
  - **Machine cap.** Read `$LanesDir\machine-lanes.json`. If it is missing, `complete` is false, or `logicalProcessors` or `memoryGB` (more than 1 GB apart) differ from this machine, run the probe first. No lane is running at that moment, so the measurement is clean. Trace `machine cap <n> lanes (probed <date>)`.
  - **Cold start.** Read `$LanesDir\auto-lanes.json`, shaped `{ "schema": 1, "lanes": 3, "savedAt": "<UTC>", "fiveHourRatePerLane": 4.0, "weeklyRatePerLane": 0.5 }`. Start at its `lanes`, or 2 if the file is missing, capped by the ceilings and at least 1. If adoption found a higher lane number, start there, as today. Trace `lanes auto: starting at <n> (<why>)`.
- **Board capacity without touching the coordinator's checkout.** The checkout (`Z:\repos\Curl`) is pulled only at shift end, and lanes run its copy of the script, so it must not move mid-shift. Keep a detached worktree at `$LanesDir\auto-board`: create it once with `git worktree add --detach`. Each step:
  1. `git -C $Root fetch -q origin <branch>`;
  2. `git -C $LanesDir\auto-board checkout -q --detach origin/<branch>`;
  3. run that worktree's own `.claude\skills\task-board\task-board.ps1 capacity` with `$env:CLAUDE_PROJECT_DIR` set to the worktree for the call, then restored;
  4. parse `^Capacity (\d+):`.

  If this fails, trace it once and use the current lane count as the capacity for that step.
- **The step.** It runs every 15 minutes inside the coordinator's wait loop (near line 1511), and is skipped while `Test-WaitingForSession`.
  1. Take a sample from `Get-UsageReading -ThisShift`. `ActiveLanes` is the active lanes that are alive and have a `lane-<n>.task`. Keep 3 hours of samples.
  2. Compute the rates. Fall back to `auto-lanes.json`'s saved rate while `Get-BurnRate` returns `$null`.
  3. Compute `Get-PaceTarget`, honouring `-NoWeeklyPace`, `-StopAtUsage` and `-StopAtWeeklyUsage`.
  4. Compute `Get-NextLaneCount`. Its `Current` is the active lanes not asked to retire.
  5. Act: up calls `Add-Lane`, down calls `Request-LaneRetire`.

  Hold without asking when `Get-UsageStop` is non-empty (reason `tokens low: <text>`) or the shift's time is up (reason `shift time up`). A change is traced in Cyan as `lanes <old> -> <new> (<limit>)`, with the `-TestAutoLanes` reason strings; a hold is traced in DarkGray. Save `auto-lanes.json` on each change and at shift end, writing a temp file and then `Move-Item -Force`.
- **`-Continuous` and `-NewTab`.** The next shift is started with `-Lanes Auto` (not the count it ended at), `-MaxLanes` and `-NoWeeklyPace` as given. Keep every parameter already forwarded, including any added by BL-760.
- **`-AutoLanesReport`: a dry run for checking the wiring without a shift.** It does the shift-start reads (never the probe; it prints `machine cap unknown (no probe yet)` instead) and one step's computation from the newest readings in `$LogDir`. It prints the machine cap, the capacity, both rates, both targets, the start count and the reason the step would log. Then it deletes the `auto-board` worktree it made and exits 0. It must not start a lane, claim or push.
- **Do not start a shift to test.** One is running now, and lanes run the coordinator's copy of the script. The change takes effect at the next shift. From a lane worktree, pass `-LogRoot Z:\repos\Curl.logs` to read real readings; `$LanesDir` then resolves beside the lane (e.g. `Z:\repos\Curl.lanes\lane-2.lanes`), which is harmless. Delete it afterwards.
- **Style.** PowerShell only, Windows PowerShell 5.1 compatible, and no new non-ASCII characters.

## Acceptance criteria

- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -AutoLanesReport -LogRoot Z:\repos\Curl.logs` exits 0 and prints these lines:
  - `machine cap`;
  - `capacity <n>`, where `<n>` matches `task-board.ps1 capacity` run on the same commit;
  - `5-hour rate`;
  - `weekly rate`;
  - `start count`;
  - one `lanes ... (...)` reason line.

  `git -C Z:\repos\Curl status --porcelain` and `git worktree list` are the same before and after.
- [x] `-TestAutoLanes` and `-TestMachineProbe` still exit 0.
- [x] `-Lanes auto -TestAlarm -QuietAlarm` and `-Lanes 16 -TestAlarm -QuietAlarm` pass parameter validation. `-Lanes 0` and `-Lanes 17` fail it.
- [x] Reading the diff shows each of these:
  - Auto always takes the coordinator path;
  - the probe runs only when `machine-lanes.json` is missing, incomplete or from different hardware;
  - capacity is read from the `auto-board` worktree, and the coordinator's checkout is not pulled mid-shift;
  - steps run every 15 minutes and skip token waits;
  - at most one `Add-Lane` or `Request-LaneRetire` happens per step;
  - `auto-lanes.json` is saved on change and at shift end;
  - `-Continuous` forwards `-Lanes Auto`;
  - every numeric use of `$Lanes` now uses the derived count.
- [x] With `-Lanes 3`, the coordinator path makes no probe, capacity or step calls (check by reading the diff).
- [x] `[System.Management.Automation.Language.Parser]::ParseFile` reports no errors for `RunDarkFactory.ps1`.
- [x] `git diff --stat` shows only `RunDarkFactory.ps1` changed outside `Tasks/`.

## Notes

- The inline `-ProbeMachine` block became `Invoke-MachineProbe`, so the Auto coordinator
  runs the same probe at shift start; `-ProbeMachine` calls it and exits as before.
- One `Get-AutoLaneStep` does the rates (saved ones while the samples give none), pace and
  next count for both the coordinator's step and `-AutoLanesReport`, so the report prints
  exactly what a step would compute. The coordinator's `Invoke-AutoLaneStep` adds the
  sampling, the holds (`tokens low: ...`, `shift time up`) and the one `Add-Lane` or
  `Request-LaneRetire`.
- `-AutoLanesReport` reads the board at this checkout's commit, or at `origin/<-Branch>`
  when `-Branch` is given, because a lane's own branch is not on origin. It deletes the
  `auto-board` worktree only when it created it, so a running shift's is left alone.
- The first sample is taken at the first step (15 minutes in), not at shift start, when
  no lane holds a task yet and the sample would drag the mean active lanes down; the saved
  rates cover the gap.
- Capacity at shift start falls back to `-MaxLanes` (no cap) when it cannot be read; mid-
  shift, as specified, to the current lane count. Only the first failure is traced.
- A step whose `Add-Lane` or `Request-LaneRetire` finds no lane traces that and saves
  nothing; the count changes only when a lane actually moves.
- Verified 2026-09-28 from lane 1: the report exits 0 with `capacity 17`, the same as
  `task-board.ps1 capacity` on commit 1c49f3fa; with a sample `auto-lanes.json` (3 lanes,
  4.0 and 0.5) it printed `lanes 3 -> 4 (weekly pace allows 4.5)`. The coordinator
  checkout's status and the worktree list were identical before and after, and the
  scratch `lane-1.lanes` folder was deleted. `-Lanes 0` and `-Lanes 17` fail validation;
  `auto` and `16` pass. `dotnet build` clean, fast tests green.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. RunDarkFactory -Lanes Auto sizes the shift: machine cap, cold start, a 15-minute one-lane step and -AutoLanesReport
