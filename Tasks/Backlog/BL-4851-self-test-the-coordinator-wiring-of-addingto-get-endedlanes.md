---
id: BL-4851
title: Self-test the coordinator wiring of -AddingTo, Get-EndedLanes and Get-ReusedLaneKeep
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/LaneState.ps1]
requirement: none
created: 2026-11-04
completed:
---
# BL-4851 — Self-test the coordinator wiring of -AddingTo, Get-EndedLanes and Get-ReusedLaneKeep


## Goal

The factory's self-tests fail if the coordinator stops using the BL-4826, BL-4848 or BL-4849 helpers. Today they test only the pure helpers, so the wiring can come out unnoticed.

## Context

Found by the shift-end review of 2026-11-04 (ADR-0710), pr-review-toolkit code-reviewer.

- **BL-4848 claim versus tests.** BL-4848's first ticked criterion says a self-test "drives that decision with a fake board". The new cases (`DarkFactory/AutoLanes.ps1:482-487`) test only `Get-LaneAddCapacity` and `Get-ClaimableReadyCount`. Nothing checks that `Get-ShiftCapacity` is called with `-AddingTo` at `DarkFactory/ShiftCoordinator.ps1:521`, `:571` and `:704`. Removing it from any of those three call sites leaves every test green.
- **BL-4826 lost its wiring check.** That `-TestHeartbeat` case (`DarkFactory/LaneState.ps1:175`) used to match `ShiftCoordinator.ps1`'s source, proving the ended-lane loop leaves the active set. After this diff it calls only `Get-EndedLanes`.
- **BL-4849 is untested at the call site.** Nothing checks that `Add-Lane` (`ShiftCoordinator.ps1:451-455`) uses `Get-ReusedLaneKeep`, labels the old tab, and that the shift-end block traces `$reusedSummaries` (`:739`).

LaneState.ps1's BL-4800 case already matches `ShiftCoordinator.ps1`'s source; follow that pattern.

## Acceptance criteria

- [ ] A `-TestHeartbeat` case in `DarkFactory/LaneState.ps1` fails when any of the three `Get-ShiftCapacity` calls in `ShiftCoordinator.ps1` (Auto step, capacity step, unpaced ramp) loses `-AddingTo`.
- [ ] A `-TestHeartbeat` case fails when the coordinator's ended-lane loop stops iterating `Get-EndedLanes`.
- [ ] A `-TestHeartbeat` case fails when `Add-Lane` stops calling `Get-ReusedLaneKeep`, stops collecting `$reusedSummaries`, or the shift-end block stops tracing them.
- [ ] `RunDarkFactory.ps1 -TestHeartbeat` passes.

## Notes

## Log

- 2026-11-04: Created by the shift-end review.
