---
id: BL-4850
title: Size the add-back lane capacity to busy lanes so an idle lane and a new lane do not chase one ready task
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/AutoLanes.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4850 — Size the add-back lane capacity to busy lanes so an idle lane and a new lane do not chase one ready task


## Goal

The ramp check, the Auto step and the fixed-count step add a lane only for a claimable ready task that no idle lane already in the shift will claim. The Auto step's log line names that capacity truthfully.

## Context

Found by the shift-end review of 2026-11-04 (ADR-0710): the pr-review-toolkit code-reviewer and silent-failure-hunter both flagged it independently.

BL-4848 added `Get-LaneAddCapacity` (`DarkFactory/AutoLanes.ps1:286-293`), which returns `min(Capacity, Current + ClaimableReady)`. `Get-ShiftCapacity -AddingTo $current` (`DarkFactory/ShiftCoordinator.ps1:334-345`) is called with every non-retiring active lane at `:521` (Auto step), `:571` (fixed-count capacity step) and `:704` (unpaced ramp). That count includes idle lanes, which are polling and will claim the same ready tasks that `ClaimableReady` counts. Example: interactive-only work in Doing inflates the board's capacity to 8. There are 4 lanes, 2 of them idle, and 1 claimable task. The add capacity comes out at `min(8, 4 + 1) = 5`, so a fifth lane is started. The idle lane takes the task, and the new lane finds nothing and ends. That is the churn BL-4848 set out to stop ("fill no lane that would find nothing and end").

Also, when capacity binds, the Auto step's reason text (`DarkFactory/AutoLanes.ps1:103`) still prints "N ready tasks can run at once". That N is now lanes plus claimable tasks, which breaks CLAUDE.md's "say what it does" rule.

The diff does not cause a regression: the old board-capacity ceiling was looser. This task fixes what remains of BL-4848's goal.

## Acceptance criteria

- [x] The add-back capacity no longer counts a claimable ready task once for an idle lane and again for a new lane. For example, it is sized to `Current + max(0, ClaimableReady - Idle)` or an equivalent, still capped at the board's capacity and never below `Current`.
- [x] Every caller that passes `-AddingTo` (Auto step, fixed-count step, unpaced ramp) supplies the idle count, or the busy count, that the new sizing needs.
- [x] A `-TestAutoLanes` case pins it: 4 lanes, 2 idle, 1 claimable task and a board capacity of 8 give an add capacity of 4, and 4 lanes with none idle and 1 claimable give 5.
- [x] The Auto step's capacity-bound reason text describes the value it prints (lanes plus claimable ready tasks, or the claimable count), not "ready tasks can run at once" for a number that is not that.
- [x] `RunDarkFactory.ps1 -TestAutoLanes` and `-TestHeartbeat` pass.

## Notes

Get-LaneAddCapacity takes -Idle (Current + max(0, ClaimableReady - Idle), min Capacity); a board capacity below Current still wins so idle lanes retire. The three callers pass the non-retiring idle count. Auto reason text now reads "N lanes can be busy at once".

## Log

- 2026-11-04: Created by the shift-end review.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 38fe2ec7 00a3a3aa 15c97c46 c8bbfacb 931a4d17.
- 2026-11-04: Doing -> Done. Add-back capacity now subtracts idle lanes; Auto reason text fixed; tests pass
