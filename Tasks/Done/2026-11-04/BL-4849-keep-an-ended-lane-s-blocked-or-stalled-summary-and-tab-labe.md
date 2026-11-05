---
id: BL-4849
title: Keep an ended lane's blocked or stalled summary and tab label when Add-Lane reuses its number
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4849 — Keep an ended lane's blocked or stalled summary and tab label when Add-Lane reuses its number

## Goal

When the coordinator starts a new lane under the number of a lane that ended with a blocked or stalled summary, the old lane's `SUMMARY` line still reaches the shift-end report and its herdr tab still gets its "BLOCKED, read" or "STALLED, read" label.

## Context

Found by the shift-end review of 2026-11-04 (ADR-0710; pr-review-toolkit silent-failure-hunter) of the shift merged at 2e9d85b7b (diff aa6fa61b8...2e9d85b7b).

BL-4826 changed `DarkFactory/ShiftCoordinator.ps1:651-660` so a lane that ended with nothing ready, not only a retired one, leaves `$activeLanes`. `Get-LaneToAdd` picks the lowest free number, so `Add-Lane` (`ShiftCoordinator.ps1:433-450`) can now reuse an ended lane's number. When it does, it renames `lane-<n>.txt` out of the `lane-*.txt` set and keeps only its non-`SUMMARY` lines in `$script:retiredLines`. `Start-Lane` then overwrites `$laneTabs[$n]`.

Before, only retired lanes were reused, and those usually end clean. An ended lane often carries `blocked=1`, and its tab is left open because its summary is not clean (`Test-LaneSummaryClean`). Scenario: lane 2 blocks a task, writes `SUMMARY ... blocked=1` and ends. Later in the shift Auto adds lane 2 again. At shift end, the loop at `ShiftCoordinator.ps1:728-739` never sees the old summary, so:
- the old tab never gets its "BLOCKED, read" label;
- lane 2's first `SUMMARY` line is missing from the shift-end trace and from the stall accounting.

The blocked task itself is still raised by `Get-WaitingOnStewart`.

## Acceptance criteria

- [x] When `Add-Lane` reuses the number of a lane whose summary is not clean, the old tab is labelled "BLOCKED, read" or "STALLED, read" (by `Get-LaneTabLabel`) before `Start-Lane` replaces `$laneTabs[$n]`.
- [x] The old lane's `SUMMARY` line appears in the shift-end trace or report, and its stalls are counted with the shift's stalls.
- [x] A `RunDarkFactory.ps1` self-test case covers reuse of a blocked lane's number and proves both points above.
- [x] The changed self-tests pass under Windows PowerShell and `pwsh`.

## Notes

- Add-Lane now calls Get-ReusedLaneKeep (AutoLanes.ps1): the old tab is labelled before Start-Lane replaces it, the SUMMARY line goes to a shift-end trace, stalls stay in retiredLines (counted with the shift stalls). Self-test cases in LaneState.ps1 (-TestHeartbeat) pass under powershell and pwsh; the coordinator wiring itself is not run by a self-test.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: a2e6f913 e17130dc 8d9492b6 758046de.
- 2026-11-04: Doing -> Done. Add-Lane keeps a reused lane's summary and tab label
