---
id: BL-766
title: Probe how many concurrent dark factory lanes this machine sustains and record the cap
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-763]
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-766 — Probe how many concurrent dark factory lanes this machine sustains and record the cap

## Goal

`RunDarkFactory.ps1 -ProbeMachine` measures how many concurrent solution builds this PC runs before they slow badly, and writes the result to `<repo>.lanes\machine-lanes.json`. The cap is then measured, not guessed. `-Lanes Auto` (BL-768) reads that file as its machine ceiling and runs the probe itself when the file is missing or stale.

## Context

- This is Stewart's design approved 2026-09-28. The ADR from BL-763 is binding, item 7 in particular. Read it first.
- **Why builds.** Every lane integrates with `Test-Green` (`RunDarkFactory.ps1`, near line 1254): `dotnet build $Root -nologo -v q`, then the fast tests. Parallel builds are the CPU and RAM peak of a shift. This PC has 32 logical processors and 125 GB RAM (2026-09-28).
- **How the probe works** (ADR item 7):
  1. **Warm-up.** One untimed `dotnet build <root> -nologo -v q --artifacts-path <dir>\probe\1`, so restore and first-build costs are not measured.
  2. **Steps.** For `k = 1, 2, ...`, start `k` concurrent `dotnet build <root> -nologo -v q --no-incremental --artifacts-path <dir>\probe\<i>` processes (`i = 1..k`) with `Start-Process -PassThru -NoNewWindow`, and wait for all of them. `--artifacts-path` gives each build its own `bin`/`obj`, so no git worktree is needed.
  3. **Measurements.** Record the step's wall time. Record the lowest free physical memory, sampled every 2 s from `(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory` (KB). Record whether every build exited 0.
  4. **Pass rule.** A step passes when its wall time is at most 2.0 times step 1's, its lowest free memory stayed at least 10% of total RAM, and every build succeeded.
  5. **Stop.** The probe stops at the first failing step, or after step 16 (the hard lane maximum), or after `-ProbeMaxLanes` (default 16, `[ValidateRange(1, 16)]`).
  6. **Cap.** The largest passing `k`, and at least 1.
  7. **Cleanup.** Delete `<dir>\probe` afterwards.
- **`machine-lanes.json`**, UTF-8, written with the temp-file-then-`Move-Item -Force` pattern:
  ```json
  { "schema": 1, "probedAt": "2026-09-28T14:02:11Z", "logicalProcessors": 32, "memoryGB": 125.6,
    "complete": true, "cap": 7, "rule": "wall <= 2.0x one build and free memory >= 10%",
    "steps": [ { "lanes": 1, "seconds": 61.2, "slowdown": 1.0, "minFreeMemoryPercent": 71.3, "succeeded": true } ] }
  ```
  - `complete` is `false` when `-ProbeMaxLanes` ended the probe before any step failed and before 16. BL-768 treats an incomplete file like a missing one.
  - `<dir>` is `$LanesDir`, so the file lands in `Z:\repos\Curl.lanes` when the coordinator runs it.
- **Pure logic, testable.** Put the pass and cap rule in `Get-MachineLaneCap -Steps -MaxLanes` (no I/O). It returns `Cap` and `Complete`. `-TestMachineProbe` runs the recorded cases below through it, printing `PASS`/`FAIL` lines, and exits 0 only when all pass. The rehearsal switches `-TestAlarm` and `-TestOutOfTokens` show the style.
- **Do not run the full probe while a shift is running.** This task most likely runs in a dark factory lane beside two others, whose builds would skew every step. That is why the coordinator runs the real probe at the start of an Auto shift, before any lane starts (BL-768).
  - The smoke check below uses `-ProbeMaxLanes 2`. Its file comes out `complete: false`, so no shift ever trusts it.
  - Run from a lane worktree, `$LanesDir` resolves beside that worktree (e.g. `Z:\repos\Curl.lanes\lane-2.lanes`). Delete that folder after the check.
- **Style.** PowerShell only, Windows PowerShell 5.1 compatible, and no new non-ASCII characters. Traces go through `Write-Trace`.

## Acceptance criteria

- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestMachineProbe` exits 0 with a `PASS` line and no `FAIL` for each case (seconds for k = 1, 2, ...; free memory 50% unless stated; all builds succeeded unless stated):
  - **knee-by-time.** 60, 62, 70, 90, 118, 125. Result: cap 5, complete.
  - **knee-by-memory.** 60, 61, 63, 64 with free memory 40, 25, 12, 8. Result: cap 3, complete.
  - **failed-build.** 60, 61, then a failed build at k = 3. Result: cap 2, complete.
  - **all-pass-to-16.** Sixteen steps of 60. Result: cap 16, complete.
  - **cut-short.** 60, 61 with `-MaxLanes 2`. Result: cap 2, not complete.
  - **first-step-fails.** k = 1 has a failed build. Result: cap 1, complete.
- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -ProbeMachine -ProbeMaxLanes 2` exits 0 and writes `machine-lanes.json` as specified. The file parses with `ConvertFrom-Json`, has `schema` 1, two `steps`, `complete` `false` (unless step 2 failed), and `logicalProcessors` equal to `[Environment]::ProcessorCount`. The `probe` folder is gone afterwards, and `git status --porcelain` is unchanged by the run.
- [ ] `-ProbeMachine` and `-ProbeMaxLanes` are documented in `param` like their neighbours. The header's `.EXAMPLE` block shows `-ProbeMachine` and `-TestMachineProbe`, and the header says in one paragraph what the probe measures and where it writes.
- [ ] `[System.Management.Automation.Language.Parser]::ParseFile` reports no errors for `RunDarkFactory.ps1`.
- [ ] `git diff --stat` shows only `RunDarkFactory.ps1` changed outside `Tasks/`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
