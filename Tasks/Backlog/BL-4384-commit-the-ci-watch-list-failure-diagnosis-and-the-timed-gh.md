---
id: BL-4384
title: Commit the CI watch list-failure diagnosis and the timed gh wrapper fix
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/CiWatch.ps1, DarkFactory/ShiftEnd.ps1, DarkFactory/Heartbeat.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-31
completed:
---
# BL-4384 — Commit the CI watch list-failure diagnosis and the timed gh wrapper fix

## Goal

The stashed work of BL-4377 (gh's error text in the `cannot list the CI runs` line, the 30-minute re-trace, `listing CI runs again`, the same in the shift-end merge wait, and the timed `gh` wrapper's array-argument fix) is applied, tested and committed so a shift runs it.

## Context

Split from BL-4377, which went over its cost cap. Its lane finished criteria 1-3 and the wrapper fix but left the code uncommitted. See BL-4377's Notes for `Invoke-GhRunList`, `Write-GhListLine`, `Wait-MergeCiRun` and `ConvertTo-NativeCallArguments`.

## Acceptance criteria

- [ ] The `cannot list` line in `DarkFactory/CiWatch.ps1` includes gh's exit code and the first line of its stderr.
- [ ] While listing keeps failing the line is written again at most every 30 minutes with how long the watch has been unable to list; the first successful list after a failure writes one `listing CI runs again` line.
- [ ] `RunDarkFactory.ps1 -TestCiWatch` covers the three cases with stand-in `-ListRuns` blocks and reports 0 FAIL.
- [ ] `ConvertTo-NativeCallArguments` in `DarkFactory/Heartbeat.ps1` joins an array argument with commas, the shift-end merge wait's `not merged: CI did not finish` line carries gh's failure, and `-TestCoordinatorWatchdog` and `-TestHeartbeat` report 0 FAIL.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4377.
- Continues the stashed work of BL-4377.

## Log

- 2026-10-31: Created.
