---
id: BL-767
title: Let the dark factory coordinator add a lane and retire a lane mid-shift
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-765]
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-767 — Let the dark factory coordinator add a lane and retire a lane mid-shift

## Goal

The coordinator can start one more lane during a shift, and can ask one lane to retire. A retiring lane finishes the task it holds, integrates and pushes, then exits cleanly, and is never killed mid-task. Restarts, adoption and the end-of-shift report keep working with a lane set that changes size. Nothing decides to scale yet; BL-768 does that. This task builds the mechanism and raises the lane maximum from 8 to 16.

## Context

- This is Stewart's design approved 2026-09-28. The ADR from BL-763 is binding, items 1 and 6 in particular. Read it first.
- **Where things are in `RunDarkFactory.ps1`:**
  - `param`: `[ValidateRange(1, 8)][int]$Lanes` (near line 143).
  - The coordinator block (near lines 1437-1583):
    - adoption scans `foreach ($n in 1..8)` (near line 1455);
    - the start loop `foreach ($n in 1..$Lanes)` creates or resets worktree `$LanesDir\lane-<n>` and calls `Start-Detached` (near lines 1484-1503);
    - the wait loop ends on `$finished -ge $Lanes` (near line 1519);
    - it restarts dead lanes over `1..$Lanes` (near line 1522);
    - the shift-end loop reads `lane-*.txt` summaries and labels or closes herdr tabs (near lines 1542-1562).
  - Lane state lives in `<repo>.logs\lanes-<stamp>\lane-<n>.<kind>` through `Get-LaneStatePath`, `Set-LaneState` and `Get-LaneState` (near line 1341).
  - The lane main loop is near line 1630. Its stop checks sit in `if (-not $resuming) { ... }`, and it ends in `Write-LaneSummary` (near line 1746).
- **What to build:**
  1. **`Start-Lane -N <n> [-Label <text>]`.** Factor the worktree preparation (create, or stash, `checkout -B` and `reset --hard`, unless the lane is adopted) and `Start-Detached` into it. It records the process or tab. The initial start, the dead-lane restart and scale-up all use it.
  2. **An active lane set.** It starts as `1..$Lanes`, including adopted lane numbers. The wait loop ends when every active lane has written its summary, instead of on the `$finished -ge $Lanes` count. Restarts consider active lanes only.
  3. **`Add-Lane`.** Takes the lowest lane number from 1 to 16 that is not active.
     - If that lane already wrote a summary this shift (it retired earlier), keep the summary's non-`SUMMARY` lines for the end-of-shift alarm. Then rename the file to `lane-<n>.retired-<HHmmss>.log`, which the `lane-*.txt` filter no longer matches.
     - Remove its `retire` and `pid` state, start it with `Start-Lane`, add it to the active set, and trace `lane <n> added`.
     - It adds nothing, and returns `$null`, when 16 lanes are active.
  4. **`Request-LaneRetire`.** Picks the highest-numbered active lane not already retiring, writes `lane-<n>.retire` (content: the time), and traces `lane <n> asked to retire after its current task`.
  5. **The lane side.** Inside the existing `if (-not $resuming)` stop checks, a lane whose `retire` state file exists stops with `$stopWhy = 'retired'`. It does not stop while resuming a held task, while running or integrating, or while a token wait holds it. It reaches the same end path as any other stop, so it writes its summary and exits 0.
  6. **Retirement seen by the coordinator.** When an active lane with a `retire` file writes its summary, the coordinator removes it from the active set, traces `lane <n> retired`, and closes its herdr tab with `Close-HerdrTab` if the summary is clean, as the shift end does today.
  7. **Shift end.** The report covers every lane started this shift, including retired ones, and the kept lines from renamed summaries join `$reasons`.
  8. **The maximum.** `-Lanes` becomes `[ValidateRange(1, 16)]`, and adoption scans lanes `1..16`.
- **Pure helpers, testable.** Add these, with no I/O:
  - `Get-LaneToAdd -Active <int[]> -Max 16`: the lowest free number, or `$null`.
  - `Get-LaneToRetire -Active <int[]> -Retiring <int[]>`: the highest active lane not retiring, or `$null`.
  - `Test-LanesFinished -Active <int[]> -Finished <int[]>`.

  Add cases for them to BL-765's `-TestAutoLanes`.
- **A fixed-lane shift must behave exactly as today.** Nothing calls `Add-Lane` or `Request-LaneRetire` yet.
- **Do not start a shift to test.** One is running now, and lanes run the coordinator's copy of the script. The change takes effect at the next shift.
- **Style.** PowerShell only, Windows PowerShell 5.1 compatible, and no new non-ASCII characters.

## Acceptance criteria

- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestAutoLanes` exits 0. Its output includes `PASS` lines for these cases, and every BL-765 case still passes:
  - `Get-LaneToAdd` over active `1,2,4` gives `3`, and over `1..16` gives `$null`.
  - `Get-LaneToRetire` over active `1,2,3` with retiring `3` gives `2`, and with retiring `1,2,3` gives `$null`.
  - `Test-LanesFinished` over active `1,3` with finished `1,2` gives false, and with finished `1,3` gives true.
- [ ] `[System.Management.Automation.Language.Parser]::ParseFile` reports no errors for `RunDarkFactory.ps1`.
- [ ] Reading the diff shows each of these:
  - the three launch sites all call `Start-Lane`;
  - the wait loop's exit and the restarts use the active set;
  - the lane checks `retire` only inside the not-resuming stop checks;
  - `Add-Lane` renames a retired lane's summary before restarting it;
  - `-Lanes` is `ValidateRange(1, 16)`, and adoption scans `1..16`.
- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -Lanes 17 -TestAlarm -QuietAlarm` fails parameter validation. `-Lanes 16 -TestAlarm -QuietAlarm` shows the banner and exits.
- [ ] The script's header "PARALLEL LANES" section says the maximum is 16, and that a lane asked to retire finishes and integrates its task first.
- [ ] `git diff --stat` shows only `RunDarkFactory.ps1` changed outside `Tasks/`.

## Notes

## Log

- 2026-09-28: Created.
