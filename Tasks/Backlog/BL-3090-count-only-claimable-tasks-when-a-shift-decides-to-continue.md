---
id: BL-3090
title: Count only claimable tasks when a shift decides to continue, size lanes or wait
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [DarkFactory/TaskIds.ps1, DarkFactory/TaskBudget.ps1, DarkFactory/Claim.ps1, DarkFactory/AutoLanesState.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3090 — Count only claimable tasks when a shift decides to continue, size lanes or wait

## Goal

One shared "claimable now" predicate (ready, not over its cost cap, not interactive-only) decides whether `-Continuous` starts another shift, how many lanes `-Lanes Auto` runs, and whether a lane waits, so a board whose only ready tasks are cost-capped never starts an empty shift.

## Context

Evidence (Arizona time): on 2026-10-22 06:02-06:15 BL-2969 had spent its per-task cost cap. Lane logs said "cost cap: its runs of the last day already spent its cost cap; not claimed" (`Invoke-Claim` in `DarkFactory/Claim.ps1`, ~line 224-225, via `Get-TaskCapSpentWhy` in `DarkFactory/TaskBudget.ps1`), yet the shift-end check in `DarkFactory/ShiftCoordinator.ps1` (~line 443-460: `$stillReady = (Invoke-Board @('next'))`, `Get-NextTaskId`, trace "work is still ready; next shift started", seen in `DarkFactory-20261022-060213.log`) still counted it as ready. `-Continuous` then started 13 empty shifts about 70 s apart (`DarkFactory-20261022-0600..0613*.log`, in `Z:\repos\Curl.logs`), each ending "nothing ready" (`ShiftRunner.ps1`). `-Lanes Auto` sizing (`Get-BoardCapacity` in `DarkFactory/AutoLanesState.ps1`, called at `ShiftCoordinator.ps1` ~line 71, parsing `task-board.ps1 capacity`) and the lane "nothing ready" wait also count capped tasks.

Do NOT edit `.claude/skills/task-board/task-board.ps1` (guard file, would make this task interactive-only). The filter lives in DarkFactory: a function such as `Test-TaskClaimableNow` / `Get-ClaimableTaskIds` in `DarkFactory/TaskIds.ps1` that takes the ids `task-board.ps1 next -Skip <capped ids>` would otherwise offer, asks `Get-TaskCapSpentWhy`, and re-asks `next` with the capped ids in `-Skip` (as `Invoke-Claim` already does) until it gets a claimable id or none. `Claim.ps1`'s loop calls the same function instead of its own inline check; the coordinator's continue decision uses it; `Get-BoardCapacity` subtracts capped ready tasks from its count (read the ready ids from `capacity` output "(BL-499, ...)" plus Doing).

Also add `Get-CapResetTime` (or similar) to `DarkFactory/TaskBudget.ps1`: the earliest time a capped task's cap frees, i.e. when its logged spend in the last 24 h (the run log timestamps `Get-TaskSpentUsd` already reads) falls so that `Test-TaskCapSpent` is false. When a shift ends with only capped tasks left, the coordinator does not start another shift; it traces "only cost-capped tasks ready; next cap frees at HH:mm" and treats the board as having nothing claimable (it ends as for "nothing ready"; BL-3092 adds the idle poll that notices when a cap frees). Keep functions pure with injected inputs so the self-tests need no board or clock.

Record the decision in a short ADR under `Documentation/Planning/Decisions/` marked "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [ ] A shared claimable-now function exists in `DarkFactory/TaskIds.ps1`, and `DarkFactory/Claim.ps1` (claim loop), `DarkFactory/ShiftCoordinator.ps1` (continue decision) and `DarkFactory/AutoLanesState.ps1` (`Get-BoardCapacity`) all call it; no second inline cap check remains.
- [ ] `RunDarkFactory.ps1 -TestTaskIds` (extended) passes with cases for: a ready uncapped task is claimable; a ready capped task is not; only capped tasks ready gives no claimable task; an interactive-only task is not.
- [ ] `RunDarkFactory.ps1 -TestTaskBudget` (extended) passes with cases for the cap-reset time: two logged runs inside 24 h give the time the oldest run leaves the window so spend drops below the cap, and no capped task gives none.
- [ ] `RunDarkFactory.ps1 -TestAutoLanes` still passes, with a case showing a board capacity of capped-only tasks sizes the shift to 0 claimable lanes' worth (no lane added for them).
- [ ] The shift-end path traces "only cost-capped tasks ready; next cap frees at <time>" and does not start a next shift when every ready task is capped (shown by a self-test of the decision function).
- [ ] `task-board.ps1` is unchanged (`git diff --stat` shows no change to it) and this task's `touches` has no guard file.
- [ ] An ADR in `Documentation/Planning/Decisions/` records the decision.
- [ ] All `RunDarkFactory.ps1 -Test*` switches that passed before still pass.

## Notes

## Log

- 2026-10-22: Created.
