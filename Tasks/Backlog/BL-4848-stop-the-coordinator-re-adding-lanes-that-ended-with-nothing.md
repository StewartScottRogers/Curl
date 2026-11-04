---
id: BL-4848
title: Stop the coordinator re-adding lanes that ended with nothing ready while only unclaimable Doing work fills the board's capacity
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftCoordinator.ps1, DarkFactory/AutoLanes.ps1]
requirement: none
created: 2026-11-04
completed:
---
# BL-4848 — Stop the coordinator re-adding lanes that ended with nothing ready while only unclaimable Doing work fills the board's capacity

## Goal

A lane that ended because nothing was ready is not started again by the Auto ramp check, the Auto step or the fixed-count capacity step until a lane-claimable task exists, so a shift whose only Doing work is interactive-only or adopted does not open and end a lane every 2 to 5 minutes.

## Context

Found by the shift-end review of 2026-11-04 (ADR-0710; pr-review-toolkit code-reviewer) of the shift merged at 2e9d85b7b (diff aa6fa61b8...2e9d85b7b).

BL-4826 changed `DarkFactory/ShiftCoordinator.ps1:651-660` so every lane that wrote its summary leaves `$activeLanes`, not only a retired one. The add-back paths size themselves to the board's capacity: the 2-minute ramp check (`ShiftCoordinator.ps1:685-695`, `Test-UnpacedRampDue` then `Invoke-AutoLaneStep`), the 15-minute Auto step, and `Invoke-CapacityLaneStep` for a fixed `-Lanes N`. That capacity is `task-board.ps1 capacity`'s count: every task in Doing (interactive-only and adopted ones included) plus the ready tasks that can start. `Get-ClaimableCapacity` (`DarkFactory/AutoLanes.ps1:159`) subtracts only unclaimable *ready* IDs, never Doing ones.

Scenario: 3 lanes running tasks, 2 interactive-only tasks in Doing, nothing ready. Capacity is 5 and 3 lanes are active, so the ramp adds 2 lanes. Each finds nothing ready, `Test-DoingMayMakeReady` (BL-4824) is false, and the lane writes its summary and ends. The next tick drops it from the active set, and 2 minutes later it is added again. Each cycle opens a herdr tab or process, resets the lane worktree, renames the summary to `lane-n.retired-*.log` and appends its lines to `$script:retiredLines`, which the shift-end alarm repeats. Before BL-4826 an ended lane stayed in `$activeLanes` and counted toward the current lanes, so this did not happen.

Two possible fixes:
- Count only the Doing tasks that active lanes hold, plus the claimable ready tasks, in the capacity the add-back paths use.
- Or re-add no lane while `Get-ClaimableTaskId` returns no Id.

## Acceptance criteria

- [ ] With nothing claimable and only interactive-only or adopted tasks in Doing, neither the ramp check, the Auto step nor `Invoke-CapacityLaneStep` adds a lane; a `RunDarkFactory.ps1` self-test case drives that decision with a fake board and proves it.
- [ ] When a claimable task appears later in the shift, a lane is still added for it (BL-4826's purpose); a self-test case proves it.
- [ ] The BL-4826 self-test case in `DarkFactory/LaneState.ps1` checks behaviour, not only a regex over `ShiftCoordinator.ps1`'s source text.
- [ ] The changed self-tests pass under Windows PowerShell and `pwsh`.

## Notes

## Log

- 2026-11-04: Created.
