---
id: BL-3510
title: Keep the no-log alarm from holding the shift-end merge and review
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/HerdrTabs.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-25
completed:
---
# BL-3510 — Keep the no-log alarm from holding the shift-end merge and review

## Goal

When a handed-over shift writes no log within 2 minutes, the alarm is raised without
stopping this coordinator from finishing the shift-end merge to `master` and the shift-end
review.

## Context

Found by the shift-end review of 2026-10-25 (diff 70a7db521...db9300a74, BL-3509;
error-handling reviewer).

`Wait-NextShiftLog` (`DarkFactory/HerdrTabs.ps1:183`) calls `$Notify`, which defaults to
`Invoke-Alarm` (`DarkFactory/Alarm.ps1:197`). In a herdr tab there is a console, so
`Test-KeyPressed` returns `$false` until a key is pressed and `Invoke-Alarm` loops - it
returns only when Stewart acknowledges it. `$startNextShift`
(`DarkFactory/ShiftCoordinator.ps1:486`) calls `Wait-NextShiftLog` synchronously:

- with `-Beside` it runs inside `$pollDuringMerge`, the `-OnPoll` of `Invoke-MergeToMaster`,
  so the shift-end merge to `master` waits on the acknowledgement (hours if Stewart is away)
  where before BL-3509 a dead next shift did not hold it;
- without `-Beside` the shift-end review (`Invoke-ShiftReview`) waits the same way.

The rest of the dark factory raises end-of-shift alarms after the work is done, not in the
middle of the merge.

## Acceptance criteria

- [ ] A next shift that writes no log within the timeout still traces the red line and
      raises the alarm, but the merge's CI wait and the shift-end review go on without a key
      press (for example: the alarm is raised after the merge and review, or the notice is
      non-blocking and the blocking alarm comes at the end of the run).
- [ ] The kept tab is still kept (`Close-OwnHerdrTab` is not called) when no log appeared.
- [ ] A self-test in `DarkFactory/HerdrTabs.ps1` or `DarkFactory/ShiftCoordinator.ps1`
      shows the hand-over returns without waiting on an acknowledgement when no log appears.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 0ea323d0 4373e12a 22e2e1cb 3d836d5e.
