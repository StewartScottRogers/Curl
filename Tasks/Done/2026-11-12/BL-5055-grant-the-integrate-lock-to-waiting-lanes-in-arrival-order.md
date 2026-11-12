---
id: BL-5055
title: Grant the integrate lock to waiting lanes in arrival order
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Claim.ps1, DarkFactory/Integrate.ps1]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5055 — Grant the integrate lock to waiting lanes in arrival order

## Goal

Lanes waiting for the integrate lock get it in the order they started waiting, with a CI fix still going before every ordinary waiter (ADR-1158), so no lane waits behind lanes that arrived after it.

## Context

`Enter-Lock` (`DarkFactory/Claim.ps1:34`) has every waiting lane retry `[IO.File]::Open` on the lock file every 3 seconds. Whoever retries at the moment the holder lets go wins, so the order is random. Claims take the same lock.

Seen in shift `lanes-20261112-130616` (2026-11-12, 13 to 15 lanes): lane 7 (BL-5012) and lane 13 (BL-5014) finished their pre-lock quality gates at 14:20 and 14:25 and were still at "waiting for the integrate lock" at 15:41. Meanwhile lanes 8, 1 and 12 met their gates later and integrated at 15:24, 15:27 and 15:29 (`Z:\repos\Curl.logs\DarkFactory-20261112-130616-L*.log`).

One approach that keeps the operating system's release-on-death: each waiter writes a ticket file (arrival time + lane) beside the lock, tries the lock only when its ticket is the oldest live one, and removes it once it holds the lock or gives up. A ticket whose lane process is gone (its `lane-<n>.pid`) or whose file is older than a few minutes without a refresh is ignored, so a dead lane never blocks the queue. The CI-fix priority marker keeps working as it does now.

## Acceptance criteria

- [x] `Enter-Lock` grants the lock in arrival order: `RunDarkFactory.ps1 -TestIntegrateLock` gains a case where three waiters queue in order A, B, C behind a holder and get the lock in that order.
- [x] A CI-fix waiter (`-Priority`) still gets the lock before ordinary waiters that arrived earlier: a `-TestIntegrateLock` case proves it.
- [x] A ticket left by a waiter that died does not block the queue: a `-TestIntegrateLock` case proves it.
- [x] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestIntegrateLock` passes, as does the same command under `pwsh`.

## Notes

Decision: tickets (`integrate.ticket-L<n>`, content = arrival ticks, refreshed every 3 s, stale after 1 minute like the priority marker) in DarkFactory/Claim.ps1; `Test-LockTurn` gates the open. Staleness by refresh, not by lane pid, because the refresh already proves liveness. Claims use -Priority so they are unchanged. Tests prove order, priority, dead ticket and cleanup by the turn predicate (three live waiters need no processes); both shells pass 33 cases.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: e021bb3f f1da603d 61734bb0 c6c4153d.
- 2026-11-12: Doing -> Done. Enter-Lock grants in arrival order via tickets
