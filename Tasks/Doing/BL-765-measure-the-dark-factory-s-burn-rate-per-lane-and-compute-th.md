---
id: BL-765
title: Measure the dark factory's burn rate per lane and compute the lane target from both usage windows
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-763]
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-765 — Measure the dark factory's burn rate per lane and compute the lane target from both usage windows

## Goal

`RunDarkFactory.ps1` gains the pure logic behind `-Lanes Auto`:
- a burn-rate meter over recorded usage samples;
- the 5-hour and weekly pace targets;
- the one-lane-per-step decision with its ceilings and log reason.

A new `-TestAutoLanes` switch proves the logic on recorded readings, with no shift, board, git or Claude involved. Nothing calls these functions in a real shift yet; BL-768 wires them in.

## Context

- This is Stewart's design approved 2026-09-28. The ADR from BL-763 is binding: items 2 (meter), 3 (targets), 4 (ceilings), 5 (step and hysteresis) and 9 (log line). Read it first.
- **Where things are in `RunDarkFactory.ps1`:**
  - `Get-UsageReading` (near line 555) returns `FiveHour`, `FiveHourResets`, `Week` and `WeekResets`. Utilization is a fraction (0.26 = 26%) and resets are local `[datetime]`.
  - `Get-UsageStop` follows it.
  - The rehearsal switches `-TestAlarm` (near line 524) and `-TestOutOfTokens` (near line 785) show the style: a `[switch]` in `param`, and an `if ($Test...) { ...; exit 0 }` block placed after the functions it needs.
- **Functions to add.** Place them in a new `# ---- auto lanes` section after `Get-UsageStop`. Keep them free of I/O: no file, git, board or `Get-Date` inside them.
  - `New-UsageSample -At -Reading -ActiveLanes`: a `[pscustomobject]` with `At`, `FiveHour`, `FiveHourResets`, `Week`, `WeekResets` and `ActiveLanes`.
  - `Get-BurnRate -Samples -Window FiveHour|Week`: percentage points per hour per active lane, or `$null`. The rules are ADR item 2.
    - `FiveHour` uses a 30-minute window and needs a span of at least 15 min.
    - `Week` uses a 180-minute window and needs at least 60 min.
    - The span runs from the oldest sample inside the window whose reset time for that window equals the newest sample's, to the newest sample.
    - Divide by the mean `ActiveLanes` over those samples. `$null` when that mean is below 0.5 or the span is too short.
    - A negative rise is 0.
  - `Get-PaceTarget -Sample -FiveHourRate -WeeklyRate -WeeklyPace [bool] -StopAtUsage -StopAtWeeklyUsage`: returns `FiveHour`, `Weekly`, `Target` and `Binding` (`'5-hour pace'` or `'weekly pace'`). Hours until reset are measured from `Sample.At`.
    - Use ADR item 3's formulas.
    - A rate of 0 or `$null`, or a reset at or before `Sample.At`, gives `[double]::PositiveInfinity` for that window.
    - A negative remaining budget gives 0.
    - With `-WeeklyPace $false` the weekly target is infinity.
    - Return `$null` when the 5-hour rate is `$null`, because there is no burn rate yet.
  - `Get-NextLaneCount -Current -Pace (object or $null) -Capacity -MachineCap -MaxLanes`: returns `Lanes`, `Changed` (bool), `Desired` (double) and `Reason`.
    - `ceiling = min(Capacity, MachineCap, MaxLanes)`.
    - The pace value is `Pace.Target`, or `Current` when `Pace` is `$null`. `desired = min(pace value, ceiling)`.
    - Up one when `desired >= Current + 1`; down one when `desired < Current - 0.25`; never below 1.
    - The binding limit is the pace when the pace value is at most the ceiling, otherwise the smallest ceiling. On a tie between ceilings, the order is capacity, machine, maximum.
- **Reason strings, exactly** (ADR item 9). Numbers use `ToString('0.0', [Globalization.CultureInfo]::InvariantCulture)`:
  - `lanes 3 -> 4 (5-hour pace allows 4.9)`
  - `lanes 4 -> 3 (weekly pace allows 1.8)`
  - `lanes 5 -> 4 (3 ready tasks can run at once)`; singular: `(1 ready task can run at once)`
  - `lanes 7 held (machine sustains 7)`
  - `lanes 2 held (lane maximum 2)`
  - `lanes 2 held (no burn rate yet)`: `Pace` is `$null` and no ceiling is below `Current`.
- **`-TestAutoLanes`.** It runs the cases below from literal data in the script. It prints `PASS <case>: <reason>` or `FAIL <case>: expected <x>, got <y>` for each, and exits 0 only when all pass, 1 otherwise. Times in the cases are local `[datetime]` literals on one arbitrary date, `D`.
- **Do not start a shift to test.** One is running now, and lanes run the coordinator's copy of the script. The change takes effect at the next shift.
- **Style.** PowerShell only, Windows PowerShell 5.1 compatible, and no new non-ASCII characters (the file is read in the system code page).

## Acceptance criteria

- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestAutoLanes` exits 0 and prints a `PASS` line for each case below and no `FAIL`. Every case uses `StopAtUsage 0.85` and `StopAtWeeklyUsage 0.97`, and weekly pacing is on unless stated.
  - **five-hour-binds.** Samples at D 12:00, 12:15 and 12:30.
    - `FiveHour` is 0.20, 0.23 and 0.26, all resetting at D 15:30.
    - `Week` is 0.10, 0.10 and 0.11, resetting at D+3 12:30.
    - `ActiveLanes` is 3 in each.
    - Current 3; capacity 6, machine 8, maximum 16.
    - Result: `lanes 3 -> 4 (5-hour pace allows 4.9)`. The weekly rate is `$null`, because the span is under 60 min.
  - **weekly-binds.** Samples at D 10:30, 12:00 and 12:30.
    - `FiveHour` is 0.10, 0.20 and 0.22, resetting at D 15:30.
    - `Week` is 0.50, 0.53 and 0.54, resetting at D+2 12:30.
    - `ActiveLanes` is 4.
    - Current 4; capacity 6, machine 8, maximum 16.
    - Result: `lanes 4 -> 3 (weekly pace allows 1.8)`.
  - **weekly-pace-off.** The same samples with weekly pacing off. Result: `lanes 4 -> 5 (6 ready tasks can run at once)`.
  - **five-hour-reset-in-window.** Samples at D 15:29, 15:44 and 15:59.
    - `FiveHour` is 0.84 (resetting at D 15:30), then 0.02 and 0.05 (resetting at D 20:30).
    - `Week` is 0.30 in all three, resetting at D+3.
    - `ActiveLanes` is 3.
    - Current 3; capacity 8, machine 8, maximum 16.
    - Result: `lanes 3 -> 4 (5-hour pace allows 4.4)`. The 15:29 sample is inside the 30-minute window but ignored for its older reset. Without that rule the rise would be negative, the rate 0, and the reason would name the capacity instead.
  - **hold-inside-band.** A 5-hour pace of 3.8 with current 4 and roomy ceilings gives `lanes 4 held (5-hour pace allows 3.8)`. A pace of 3.7 gives `lanes 4 -> 3 (5-hour pace allows 3.7)`.
  - **capacity-ceiling.** Current 5, pace 9.0, capacity 3 gives `lanes 5 -> 4 (3 ready tasks can run at once)`. Capacity 1 with current 2 and no pace gives `lanes 2 -> 1 (1 ready task can run at once)`.
  - **machine-ceiling.** Current 7, infinite pace, capacity 20, machine 7, maximum 16 gives `lanes 7 held (machine sustains 7)`.
  - **max-ceiling.** Current 2, infinite pace, capacity 20, machine 16, maximum 2 gives `lanes 2 held (lane maximum 2)`.
  - **no-rate.** Current 2, `Pace` `$null`, capacity 6 gives `lanes 2 held (no burn rate yet)`.
  - **idle-lanes.** Samples whose `ActiveLanes` are all 0 give a `$null` 5-hour rate.
- [ ] `RunDarkFactory.ps1` has a `[switch]$TestAutoLanes` documented in `param` like its neighbours, and a `-TestAutoLanes` line in the header's `.EXAMPLE` block.
- [ ] None of the new functions reads a file, runs git, the board or Claude, or calls `Get-Date` (check by reading the diff).
- [ ] `[System.Management.Automation.Language.Parser]::ParseFile` reports no errors for `RunDarkFactory.ps1`. `-TestAlarm -QuietAlarm` still shows its banner and exits.
- [ ] `git diff --stat` shows only `RunDarkFactory.ps1` changed outside `Tasks/`.

## Notes

- Arithmetic behind the expected numbers:
  - five-hour-binds: 6 points / 0.5 h / 3 lanes = 4.0, then 59 / (4.0 x 3 h) = 4.92.
  - weekly-binds: weekly 4 / 2 h / 4 = 0.5, then 43 / (0.5 x 48 h) = 1.79. Its 5-hour target is 2 / 0.5 / 4 = 1.0, then 63 / (1.0 x 3) = 21.0.
  - reset case: 3 / 0.25 h / 3 = 4.0, then 80 / (4.0 x 4.52 h, from 15:59 to 20:30) = 4.43.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
