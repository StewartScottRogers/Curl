# ADR-0757 — A shift counts only tasks claimable now when it continues, sizes lanes or claims

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3090
- Decided by Claude under Stewart's delegation.

## Context

On 2026-10-22 from 06:02 to 06:15 BL-2969 had spent its per-task cost cap. Each lane's claim
passed it over ("cost cap ... not claimed"), but the shift-end check asked `task-board.ps1 next`
alone, found BL-2969 ready, and `-Continuous` started 13 empty shifts about 70 seconds apart.
`-Lanes Auto` sized its lanes from `task-board.ps1 capacity`, which counted the capped task too.
`task-board.ps1` is a guard file a lane may not change, so the filter has to live in DarkFactory.

## Decision

- One predicate, `Test-TaskClaimableNow` in `DarkFactory/TaskIds.ps1`, says whether a ready task
  may be claimed now: it is not over its cost cap (`Get-TaskCapSpentWhy`) and is not interactive
  only (`lane: no`). `Get-TaskNotClaimableWhy` applies it to a task on the board and says why not.
- `Get-ClaimableTaskId` asks `next`, and asks again with each unclaimable task in `-Skip`, until it
  gets a claimable task or none; it reports the cost-capped tasks it passed over.
- The claim loop (`Claim.ps1`) uses `Get-TaskNotClaimableWhy` in place of its own cap check.
- The shift end (`ShiftCoordinator.ps1`) starts the next `-Continuous` shift only when
  `Get-ClaimableTaskId` finds a claimable task (`Get-ShiftEndDecision`). When only cost-capped
  tasks are ready it traces "only cost-capped tasks ready (...); next cap frees at HH:mm" and ends
  as for nothing ready; BL-3092 adds the idle poll that notices when a cap frees.
- `Get-CapResetTime` (`TaskBudget.ps1`) gives the time a capped task's oldest runs leave the
  24-hour window so the rest no longer spends its cap; `Get-CapFreesAt` takes the earliest over
  the capped tasks. The cap used is today's, though the median behind it may move by then.
- `Get-BoardCapacity` subtracts the ready tasks the capacity line names that are not claimable
  now (`Get-ClaimableCapacity` in `AutoLanes.ps1`), so `-Lanes Auto` adds no lane for them.

## Consequences

A board whose only ready tasks are capped ends the shift instead of restarting it. The check a
claimed task's run makes before it starts (`ShiftRunner.ps1`) stays: it guards a task claimed by
another route (a resume), not the choice of which task to claim.
