# ADR-0130 — `-Lanes Auto` paces dark factory lanes to the usage windows, the board and the machine

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-763.
Stewart approved the overall design on 2026-09-28: the dark factory chooses its own lane
count from the measured burn rate, the task board and the machine, so it uses what the
current Claude plan can sustain and nobody retunes it after a plan change. This ADR fixes
the details that BL-764 to BL-770 build against.

## Context

Stewart moves between Claude plans (Max 5X to Max 10X, and back as affordability
dictates). A fixed `-Lanes N` is right for one plan and wrong for the next: too few lanes
leave tokens unspent, too many hit the 85% five-hour stop early and idle until the reset.

`Get-UsageReading` (`RunDarkFactory.ps1`) already reads the `five_hour` and `seven_day`
utilization and their `resetsAt` from each run's `rate_limit_event`. Utilization is a
fraction of the current plan, so the factory never needs to know the tier: it measures
how fast one lane spends the plan and sizes from that.

Measured on 2026-09-28 in `Z:\repos\Curl.logs\BL-580-20260928-120958-L3.jsonl`:

- A run logs a `rate_limit_event` several times: 4 times in 262 lines.
- Utilization comes in whole hundredths, e.g. `0.02` then `0.03`, so one reading step is
  one percentage point.
- The weekly window moves slowly (`0` to `0.01` inside one run). A 30-minute difference of
  the weekly reading is mostly rounding noise, so the weekly rate needs a longer span than
  the five-hour rate.

The machine, measured the same day with `Win32_Processor` and `Win32_ComputerSystem`: 32
logical processors and 125 GB of RAM. The script's `-Lanes` is `[ValidateRange(1, 8)]`.
That range is a script parameter, not a `CodeMetricsConfig.txt` threshold, so it is
Claude's to change.

ADR-0129 (BL-762) defines `status.json` schema 1, whose readers ignore fields they do not
know.

## Decision

1. **`-Lanes` accepts `Auto` or a whole number 1 to 16.** A number keeps today's fixed
   behaviour. `Auto` always runs the coordinator with lane worktrees, even at one lane.
   `-MaxLanes` (default 16, range 1 to 16) is Auto's lane maximum. The fixed range rises
   from 8 to 16 so the probe, not a guess, sets the practical ceiling. `Auto` becomes the
   start command `CLAUDE.md` recommends.

2. **Burn-rate meter.** Every 15 minutes (the *step*) the coordinator records a sample:
   - `At`;
   - `FiveHour` and `FiveHourResets`;
   - `Week` and `WeekResets`;
   - `ActiveLanes`: lanes that are alive, hold a task, and are not waiting for tokens.

   It skips the step while the shift waits for a session, and keeps the samples of the
   last 3 hours.

   A rate is the rise in utilization, in percentage points, over a span, divided by the
   span in hours and by the mean `ActiveLanes` of the samples in that span. The span runs
   from the oldest kept sample inside the window, with the same `resetsAt` for that window
   as the newest sample, to the newest sample. A negative rise counts as 0. There is no
   rate when the mean active lanes is below 0.5.
   - The five-hour rate uses a 30-minute window and needs a span of at least 15 minutes.
   - The weekly rate uses a 3-hour window and needs a span of at least 60 minutes,
     because the weekly reading moves one point at a time.

3. **Two pacing targets**, in lanes, both fractional:
   - `fiveHourTarget = (StopAtUsage - FiveHour) * 100 / (fiveHourRate * hoursUntilFiveHourReset)`.
     That is the lane count that reaches about 85% just as the five-hour window resets.
   - `weeklyTarget = (StopAtWeeklyUsage - Week) * 100 / (weeklyRate * hoursUntilWeekReset)`.
     That spreads what is left of the weekly budget up to 97% evenly until the weekly
     reset. It assumes the factory runs around the clock under `-Continuous`; a factory
     that runs less than that under-spends, which is the safe side.

   A rate of 0, or a reset that is due now, makes that target unbounded.

   The pace target is `min(fiveHourTarget, weeklyTarget)` with `-WeeklyPace`, and
   `fiveHourTarget` alone without it, which is the default since the BL-806 amendment
   below: burn at the 5-hour pace and stop claiming at 97% weekly.

4. **Ceilings.** `ceiling = min(capacity, machineCap, MaxLanes)`.
   - `capacity` comes from the board's new `capacity` command (BL-764): the tasks in
     `Doing` plus the ready tasks that could start alongside them without overlapping
     `touches`.
   - `machineCap` comes from the probe (item 7).

5. **One step, with hysteresis.** Let `desired = min(pace target, ceiling)`. Scale up by
   one when `desired >= current + 1`. Scale down by one when `desired < current - 0.25`.
   Otherwise hold. There is never fewer than 1 lane.

   These cases hold too:
   - no five-hour rate is known yet (no samples and none saved);
   - `Get-UsageStop` already says stop, because lanes are winding down;
   - the shift's time is up.

6. **Scaling mechanics.**
   - Scaling up starts the lowest free lane number through the existing `Start-Detached`
     path: a herdr tab, or a console window outside herdr.
   - Scaling down marks the highest-numbered active lane to retire with a
     `lane-<n>.retire` state file. At the top of its loop, before its next claim, that
     lane stops with `retired`: it writes its summary and exits.
   - A lane never retires mid-task, mid-integration, or while resuming a held task.
   - Stopped and adopted lanes keep today's semantics.

7. **Machine-cap probe.** It runs `k = 1, 2, 3, ...` concurrent
   `dotnet build <root> -nologo -v q --no-incremental --artifacts-path <LanesDir>\probe\<i>`
   builds of the same checkout. No worktrees are needed.
   - Each step records the wall time and the lowest free physical memory seen.
   - `machineCap` is the largest `k` whose wall time is at most 4.0 times the one-build
     time and whose free memory stayed at least 20% of RAM (and whose builds all
     succeeded), and at least 1. (It was 2.0 times and 10% until the BL-807 amendment.)
   - The probe stops at the first `k` that fails either test, or at 16, the hard lane
     maximum.
   - A probe cut short by `-ProbeMaxLanes` before any step failed is marked incomplete.
   - It writes `<repo>.lanes\machine-lanes.json`, UTF-8, temp file then `Move-Item -Force`:

     ```json
     { "schema": 1, "probedAt": "2026-09-28T14:02:11Z", "logicalProcessors": 32, "memoryGB": 125.6,
       "complete": true, "cap": 7, "rule": "wall <= 4.0x one build and free memory >= 20%",
       "steps": [ { "lanes": 1, "seconds": 61.2, "slowdown": 1.0, "minFreeMemoryPercent": 71.3, "succeeded": true } ] }
     ```

     | Field | Meaning |
     | --- | --- |
     | `schema` | 1. |
     | `probedAt` | UTC time the probe finished. |
     | `logicalProcessors` | `[Environment]::ProcessorCount` when probed. |
     | `memoryGB` | Physical RAM when probed, one decimal. |
     | `complete` | `false` when `-ProbeMaxLanes` ended the probe before any step failed and before 16. |
     | `cap` | `machineCap`. |
     | `rule` | The pass rule, as text. |
     | `steps[]` | One per `k`: `lanes`, `seconds` (wall time), `slowdown` (`seconds` over the one-build time), `minFreeMemoryPercent`, `succeeded`. |

   - The coordinator runs the probe at the start of an Auto shift, before any lane
     starts, whenever that file is missing, is marked incomplete, names a different
     logical processor count or a RAM size more than 1 GB apart, or records a `rule`
     other than the current one.
   - The finding is recorded on the machine, not in the repository, because it describes
     one PC.

8. **Cold start.** `<repo>.lanes\auto-lanes.json` is written, temp file then
   `Move-Item -Force`, at every lane change and at shift end:

   ```json
   { "schema": 1, "lanes": 3, "savedAt": "2026-09-28T18:30:00Z", "fiveHourRatePerLane": 4.0, "weeklyRatePerLane": 0.5 }
   ```

   | Field | Meaning |
   | --- | --- |
   | `schema` | 1. |
   | `lanes` | The lane count when saved. |
   | `savedAt` | UTC time of the save. |
   | `fiveHourRatePerLane` | Last five-hour rate, percentage points per hour per lane, or `null`. |
   | `weeklyRatePerLane` | Last weekly rate, percentage points per hour per lane, or `null`. |

   - An Auto shift starts at `lanes`, raised to `-MinStartLanes` (default 3) when that is
     more, capped by the ceilings, and meters from the saved rates until it has its own.
   - The first-ever start (no file) is `-MinStartLanes` lanes. See the amendment below.
   - When adopted lanes number more than the start count, the shift starts at the highest
     adopted lane number, as today.

9. **The log line.** Every change is traced as `lanes <old> -> <new> (<binding limit>)`.
   The binding limit is one of:
   - `weekly pace allows 5.2`
   - `5-hour pace allows 4.7`
   - `6 ready tasks can run at once`
   - `machine sustains 7`
   - `lane maximum 16`

   A hold is traced in dark grey as `lanes 4 held (<binding limit>)`. Numbers have one
   decimal, invariant culture. Stewart's example in the request, `lanes 3 -> 5`, moves two
   lanes at once and conflicts with the one-lane step (item 5), so the step wins.

10. **`status.json` gains a top-level `autoLanes` object** (BL-770), additive to schema 1:
    `{ "lanes": 4, "target": 5.2, "binding": "weekly pace", "reason": "lanes 3 -> 4 (weekly pace allows 5.2)", "changedAt": "<UTC>" }`.
    It is `null` for a fixed-lane shift. The schema stays 1 because readers ignore unknown
    fields (ADR-0129).

## Consequences

- The factory spends what the current plan can sustain with no per-plan setting; a plan
  change shows up as a different measured rate within one or two steps.
- The work is split so each piece lands against this design:
  - BL-764 adds the board's `capacity` command (item 4).
  - BL-765 measures the burn rate per lane and computes both targets (items 2 and 3).
  - BL-766 adds the machine-cap probe and `machine-lanes.json` (item 7).
  - BL-767 lets the coordinator add a lane and retire one mid-shift (item 6).
  - BL-768 runs `-Lanes Auto`: the parameter change, cold start, one-step scaling and log
    line (items 1, 5, 8 and 9).
  - BL-769 documents `-Lanes Auto` as the way to start a shift in `CLAUDE.md`.
  - BL-770 publishes `autoLanes` in `status.json` and shows it on the task board page
    (item 10).
- One-lane steps every 15 minutes are slow to react: going from 2 to 8 lanes takes 90
  minutes. That is the price of not overshooting on a whole-point meter.
- Weekly pacing assumes around-the-clock running; a factory that runs less under-spends
  the week rather than running it dry.
- The first Auto shift on a machine, and any shift after a hardware change, spends a few
  minutes on the probe before a lane starts.

## Alternatives considered

- **A per-plan lane table.** It needs retuning whenever the plan changes, which is the
  problem this decision removes.
- **Scaling several lanes per step.** It overshoots on a noisy, whole-point meter.
- **Killing a surplus lane.** It loses work mid-task; retiring at the top of the loop
  loses none.
- **Guessing the machine cap from the processor count.** Builds are memory and disk
  bound too, so only a measurement is trustworthy.
- **Worktrees for the probe.** `--artifacts-path` isolates the outputs without git.

## Amendment 2026-09-28: a starting count of at least three lanes (BL-780)

Stewart asked for at least three lanes, and chose a starting count over a hard floor. An
Auto shift now starts at the larger of the saved `lanes` and `-MinStartLanes` (default 3),
still capped by the board's capacity, the machine cap and `-MaxLanes`. The start trace
says when the rule applied, e.g. `last shift saved 2, raised to 3 by -MinStartLanes`.

It is not a floor. After the start, Auto steps as before (item 5) and retires below
`-MinStartLanes` when the pace demands it, because a fixed three lanes is what ran the
tokens out on Max 5X. A fixed `-Lanes N` ignores `-MinStartLanes`, and `-Continuous`
hands it on to the next shift. `-TestAutoLanes` covers the rule.

## Amendment 2026-09-28: the weekly window stops claims instead of pacing lanes (BL-806)

Decided by Claude under Stewart's delegation, after debugging a shift with him; he said
"go" to this change.

At 18:00 on 2026-09-28 the weekly pace retired lane 3. The week was 13% used, it reset
on Wednesday at 06:00, and the meter read 0.885 points per lane-hour, so
`(97 - 13) / (0.885 * 36) = 2.6`. The arithmetic was right, but pacing the weekly window
was the wrong goal:

- Pacing cannot finish more work than burning to 97% and then waiting. The budget spent
  by the reset is the same either way.
- It finishes less whenever the factory idles: an empty board, a wait for the 5-hour
  window, a restart. What is left at the reset is lost. Item 3's "under-spends, which is
  the safe side" treated that loss as safe.

The default is now the old `-NoWeeklyPace` behaviour:

- Lanes pace to the 5-hour window, the board and the machine.
- Lanes stop claiming at `-StopAtWeeklyUsage` (97%). The 3% left is Stewart's for
  interactive work.
- A shift that finds the week used up waits for the weekly reset with the ordinary
  notice. The alarm is for problems, and running out is the plan working.

`-WeeklyPace` opts back into item 3's weekly target, for a week when Stewart wants tokens
kept back.

## Amendment 2026-09-28: the machine cap knee moves to 4x and 20% free memory (BL-807)

Decided by Claude under Stewart's delegation. The first probe on this 32-processor,
125.6 GB PC capped the factory at 3 lanes: four simultaneous builds took 36.7 s against
13.6 s for one (2.7x), with 71.5% of memory still free. That cap came from a worst case
that almost never happens:

- A lane builds for a few minutes of each task.
- Lanes integrate one at a time under `integrate.lock`.

So simultaneous builds rarely line up, and when they do, a slower build costs minutes of
a task that takes most of an hour. Memory is the real limit, because running out stalls
everything.

The rule is now 4.0x the one-build time and at least 20% of memory free. A
`machine-lanes.json` recorded under another rule is probed again at the next Auto shift.
