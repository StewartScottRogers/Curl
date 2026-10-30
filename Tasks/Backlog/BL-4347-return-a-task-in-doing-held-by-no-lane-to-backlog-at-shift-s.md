---
id: BL-4347
title: Return a task in Doing held by no lane to Backlog at shift start when no work for it exists anywhere
priority: High
assignee: Claude
pipeline: bugfix
depends-on: []
touches: [DarkFactory/Park.ps1, DarkFactory/ShiftCoordinator.ps1, DarkFactory/ShiftRunner.ps1]
requirement: none
created: 2026-10-30
completed:
---
# BL-4347 — Return a task in Doing held by no lane to Backlog at shift start when no work for it exists anywhere

## Goal

A shift start that finds a task in Doing held by no lane, with no work for it anywhere, returns it to Backlog and starts, instead of refusing and stopping the factory until someone clears it.

## Context

On 2026-10-30 lane 13 claimed BL-4342 (commit 6e0ea0d10) just before the shift ended at 02:48 Arizona time ("lane 13 ended (time up)"). Its worktree `Curl.lanes\lane-13` was clean with no commits ahead of origin, and no `factory/BL-4342-lane-*` branch existed. The `-Continuous` hand-over at 02:49 refused: "BL-4342 is in Doing and held by no lane, no lane worktree has uncommitted work and no parked branch factory/BL-4342-lane-* exists", and no shift ran until an interactive session moved it to Backlog at 06:06 - over three hours of 13 idle lanes (the alarm was silent: audio is off).

`Get-OrphanAction` in `DarkFactory/Park.ps1` (BL-1071) returns 'backlog' only when a parked branch holds the task; with no dirty worktree and no parked branch it returns 'refuse'. With no work anywhere there is nothing to lose, so that case should return the task to Backlog like the parked case does (`ShiftCoordinator.ps1` already does the move, commit and push for 'backlog'). Before deciding there is no work, also check the lane branches (`factory/lane-<n>`) for commits not on the shift branch; if any lane branch has such commits, keep refusing, since which lane's work it is cannot be told.

Also find out why a lane could hold a fresh claim at shift end (the runner checks `$shiftEnd` before `Invoke-Claim` in `DarkFactory/ShiftRunner.ps1`; the coordinator may stop lanes on time-up while one is between claim and run). If a lane stopped at time-up holds a claim it never started, return the claim to Backlog as it ends.

## Acceptance criteria

- [ ] `Get-OrphanAction` returns 'backlog' (with no branch) for a task with no dirty lane worktree, no parked branch and no lane branch holding commits beyond the shift branch, and still returns 'refuse' when a lane branch has such commits; the factory's self-test (`-Test*` switch covering Park.ps1) proves each case.
- [ ] Shift start with such a task traces the requeue, commits and pushes the task's move to Backlog, and starts the shift.
- [ ] A lane stopped at time-up never leaves an unstarted claim in Doing: either it cannot claim past the deadline, or it returns the claim to Backlog as it ends; the self-test or the trace of a rehearsal shows it.
- [ ] `DarkFactory/LaneRules.md` and the script help say what now happens, if either describes the old refusal.

## Notes

## Log

- 2026-10-30: Created.
