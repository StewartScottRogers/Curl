---
id: BL-763
title: Decide how -Lanes Auto paces dark factory lanes to the usage windows, the board and the machine
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-763 — Decide how -Lanes Auto paces dark factory lanes to the usage windows, the board and the machine

## Goal

An ADR records how `RunDarkFactory.ps1 -Lanes Auto` chooses how many lanes to run. It covers the burn-rate meter, the two pacing targets, the ceilings, the one-lane step, the cold start, the machine-cap probe and the log line. BL-764 to BL-770 then build against one written design.

## Context

- **Approval.** Stewart approved the design on 2026-09-28. He moves between Claude plans (Max 5X to Max 10X, and back as affordability dictates). The factory must use what the current plan can sustain, and nobody retunes it after a plan change.
- **Plan-agnostic by construction.** `Get-UsageReading` (`RunDarkFactory.ps1`, near line 555) already reads `five_hour` and `seven_day` utilization and their `resetsAt` from each run's `rate_limit_event`. The utilization is a fraction of the current plan, so the factory never needs to know the tier. It measures the burn rate instead.
- **Measured 2026-09-28 by the planner** in `Z:\repos\Curl.logs\BL-580-20260928-120958-L3.jsonl`:
  - A run logs a `rate_limit_event` several times, 4 times in 262 lines.
  - Utilization comes in whole hundredths, e.g. `0.02` then `0.03`, so one reading step is one percentage point.
  - The weekly window moves slowly (`0` to `0.01` inside one run). A 30-minute difference of the weekly reading is therefore mostly rounding noise, and the weekly rate needs a longer span than the 5-hour rate.
- **The machine.** 32 logical processors and 125 GB of RAM (`Win32_Processor`, `Win32_ComputerSystem`, 2026-09-28). The script's `-Lanes` is `[ValidateRange(1, 8)]`. That is a script parameter, not a `CodeMetricsConfig.txt` threshold, so Claude may change it.
- **What already exists.** BL-762's ADR defines `status.json` schema 1. Its readers ignore fields they do not know.
- **Decisions reached while planning.** Record them under Decision as numbered items. A detail may be improved only if the ADR says why.
  1. **`-Lanes` accepts `Auto` or a whole number 1 to 16.** A number keeps today's fixed behaviour. `Auto` always runs the coordinator with lane worktrees, even at one lane. `-MaxLanes` (default 16, range 1 to 16) is Auto's lane maximum. The fixed range rises from 8 to 16 so the probe, not a guess, sets the practical ceiling. `Auto` becomes the start command CLAUDE.md recommends.
  2. **Burn-rate meter.** Every 15 minutes (the *step*) the coordinator records a sample with these fields:
     - `At`;
     - `FiveHour` and `FiveHourResets`;
     - `Week` and `WeekResets`;
     - `ActiveLanes`: lanes that are alive, hold a task, and are not waiting for tokens.

     It skips the step while the shift waits for a session. It keeps the samples of the last 3 hours.

     A rate is the rise in utilization, in percentage points, over a span, divided by the span in hours and by the mean `ActiveLanes` of the samples in that span. The span runs from the oldest kept sample inside the window, with the same `resetsAt` for that window as the newest sample, to the newest sample. A negative rise counts as 0. There is no rate when the mean active lanes is below 0.5.
     - The 5-hour rate uses a 30-minute window and needs at least 15 minutes.
     - The weekly rate uses a 3-hour window and needs at least 60 minutes, because the weekly reading moves one point at a time.
  3. **Two pacing targets**, in lanes, both fractional:
     - `fiveHourTarget = (StopAtUsage - FiveHour) * 100 / (fiveHourRate * hoursUntilFiveHourReset)`. That is the lane count that reaches about 85% just as the 5-hour window resets.
     - `weeklyTarget = (StopAtWeeklyUsage - Week) * 100 / (weeklyRate * hoursUntilWeekReset)`. That spreads what is left of the weekly budget up to 97% evenly until the weekly reset. It assumes the factory runs around the clock under `-Continuous`; a factory that runs less than that under-spends, which is the safe side.

     A rate of 0, or a reset that is due now, makes that target unbounded.

     The pace target is `min(fiveHourTarget, weeklyTarget)`. Weekly pacing is the default. `-NoWeeklyPace` drops the weekly target: burn fast and stop early at 97% weekly, as today.
  4. **Ceilings.** `ceiling = min(capacity, machineCap, MaxLanes)`.
     - `capacity` comes from the board's new `capacity` command (BL-764): the tasks in Doing plus the ready tasks that could start alongside them without overlapping `touches`.
     - `machineCap` comes from the probe (item 7).
  5. **One step, with hysteresis.** Let `desired = min(pace target, ceiling)`. Scale up by one when `desired >= current + 1`. Scale down by one when `desired < current - 0.25`. Otherwise hold. There is never fewer than 1 lane.

     Other cases hold too:
     - no 5-hour rate is known yet (no samples and none saved);
     - `Get-UsageStop` already says stop, because lanes are winding down;
     - the shift's time is up.
  6. **Scaling mechanics.**
     - Scaling up starts the lowest free lane number through the existing `Start-Detached` path: a herdr tab, or a console window outside herdr.
     - Scaling down marks the highest-numbered active lane to retire with a `lane-<n>.retire` state file. At the top of its loop, before its next claim, that lane stops with `retired`: it writes its summary and exits.
     - A lane never retires mid-task, mid-integration, or while resuming a held task.
     - Stopped and adopted lanes keep today's semantics.
  7. **Machine-cap probe.** It runs `k = 1, 2, 3, ...` concurrent `dotnet build <root> -nologo -v q --no-incremental --artifacts-path <LanesDir>\probe\<i>` builds of the same checkout. No worktrees are needed.
     - Each step records the wall time and the lowest free physical memory seen.
     - `machineCap` is the largest `k` whose wall time is at most 2.0 times the one-build time and whose free memory stayed at least 10% of RAM.
     - The probe stops at the first `k` that fails either test, or at 16, the hard lane maximum.
     - A probe cut short by `-ProbeMaxLanes` before any step failed is marked incomplete.
     - It writes `<repo>.lanes\machine-lanes.json`.
     - The coordinator runs the probe at the start of an Auto shift, before any lane starts, whenever that file is missing, is marked incomplete, or names a different logical processor count or RAM size.
     - The finding is recorded on the machine. It is not in the repository, because it describes one PC.
  8. **Cold start.** `<repo>.lanes\auto-lanes.json` is written at every change and at shift end. It keeps the lane count and the last 5-hour and weekly per-lane rates.
     - An Auto shift starts at that count, capped by the ceilings, and meters from the saved rates until it has its own.
     - The first-ever start is 2 lanes.
     - When adopted lanes number more than the start count, the shift starts at the highest adopted lane number, as today.
  9. **The log line.** Every change is traced as `lanes <old> -> <new> (<binding limit>)`. The binding limit is one of:
     - `weekly pace allows 5.2`
     - `5-hour pace allows 4.7`
     - `6 ready tasks can run at once`
     - `machine sustains 7`
     - `lane maximum 16`

     A hold is traced in dark grey as `lanes 4 held (<binding limit>)`. Numbers have one decimal, invariant culture. Stewart's example in the request, `lanes 3 -> 5`, moves two lanes at once and conflicts with the one-lane step, so the step wins.
  10. **`status.json` gains a top-level `autoLanes` object** (BL-770), additive to schema 1: `{ "lanes": 4, "target": 5.2, "binding": "weekly pace", "reason": "lanes 3 -> 4 (weekly pace allows 5.2)", "changedAt": "<UTC>" }`. It is `null` for a fixed-lane shift. The schema stays 1 because readers ignore unknown fields.
- **Rejected alternatives.** List these:
  - A per-plan lane table: it needs retuning whenever the plan changes.
  - Scaling several lanes per step: it overshoots on a noisy, whole-point meter.
  - Killing a surplus lane: it loses work mid-task.
  - Guessing the machine cap from the processor count: builds are memory and disk bound too.
  - Worktrees for the probe: `--artifacts-path` isolates the outputs without git.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists, with the number checked as unused at the time of writing (lanes add ADRs concurrently). Its Status is Accepted, and it is marked "Decided by Claude under Stewart's delegation", noting that Stewart approved the overall design on 2026-09-28.
- [ ] Its Context holds the 2026-09-28 measurements above: the reading resolution, the events per run, and the machine.
- [ ] Its Decision states items 1 to 10, with the formulas written exactly as above and the `machine-lanes.json` and `auto-lanes.json` fields listed.
- [ ] It lists the rejected alternatives above.
- [ ] Its Consequences name BL-764, BL-765, BL-766, BL-767, BL-768, BL-769 and BL-770.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.
- [ ] `git diff --stat` shows only `Documentation/Planning/Decisions` changed outside `Tasks/`.

## Notes

## Log

- 2026-09-28: Created.
