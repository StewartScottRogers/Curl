---
id: BL-3142
title: Start the next -Continuous shift while the shift-end merge waits for CI, once a task is claimable
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftCoordinator.ps1, DarkFactory/ShiftEnd.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3142 — Start the next -Continuous shift while the shift-end merge waits for CI, once a task is claimable

## Goal

A `-Continuous` coordinator whose shift ended with nothing claimable starts the next shift within `-IdlePollMinutes` of a task becoming claimable, even while its shift-end merge is still waiting for CI.

## Context

Shift 20261022-140915 ended at 14:17:53 with nothing ready and logged "Audit due" at 14:18:09. Then it sat in `Invoke-MergeToMaster` (`DarkFactory/ShiftEnd.ps1`), which waits up to `$MergeCiWaitMinutes` (60) for CI on the branch head. `DarkFactory/ShiftCoordinator.ps1` reaches the idle poll (BL-3092, ADR-0758, its "nothing claimable; polling the board" trace) only after the merge returns. 25 tasks (BL-3117..BL-3141, 6 claimable at once) landed at about 14:28 and waited unclaimed until an interactive `-Restart` at 14:39. The restart also skipped that shift's merge.

Where to start: the end-of-shift sequence in `ShiftCoordinator.ps1` (Invoke-MergeToMaster at "$mergeLine = ...", then the review, then the idle-poll loop). One way to do it: check `Get-ClaimableTaskId` every `-IdlePollMinutes` during the merge's CI wait, and once a task is claimable start the next shift through the one hand-over (`$startNextShift`), then finish the merge and review beside it, as the review already runs beside the next shift (ADR-0710).

## Acceptance criteria

- [ ] While the shift-end merge waits for CI under `-Continuous`, the coordinator checks the board every `-IdlePollMinutes` and starts the next shift once a task is claimable. The merge still completes (or reports why not) after the hand-over, and only one next shift is ever started.
- [ ] A `RunDarkFactory.ps1 -Test*` self-test pins it: with the merge's CI wait stubbed as pending and a task claimable at the second poll, the next shift is started once and the merge line is still traced.
- [ ] The comment above the idle poll in `ShiftCoordinator.ps1` and ADR-0758 say the poll also runs during the merge wait.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
