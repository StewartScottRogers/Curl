---
id: BL-4425
title: Keep a Blocked task Blocked when its committed code fails the shared-branch verify
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Integrate.ps1, DarkFactory/ShiftRunner.ps1, DarkFactory/Park.ps1]
requirement: none
created: 2026-10-31
completed:
---
# BL-4425 — Keep a Blocked task Blocked when its committed code fails the shared-branch verify

## Goal

A lane run that ends its task in `Blocked` (or `Deferred`) still leaves it there on the shared branch, with the shift-end alarm, when the code it committed fails the shared-branch build and fast tests; only the code is parked.

## Context

Found by the shift-end review of 2026-10-31 (ADR-0710; silent-failure-hunter, Important) in the merge `55fa1ce15...ac0e3e86e` (AF-0496's change).

- `DarkFactory/Integrate.ps1:236-245`: a run whose task went to Backlog or Blocked now has its committed code verified on the shared branch whenever it changes more than task files. When that verify is red, `Invoke-Integrate` returns the problem.
- `DarkFactory/ShiftRunner.ps1:228-234`: any problem from `Invoke-Integrate` calls `Invoke-Park`, which (`DarkFactory/Park.ps1:20-27`) syncs to the shared branch, sees the task still in `Doing` there (the lane's Blocked move was never pushed) and moves it to `Backlog`; `$state` becomes `Parked`.
- So a task the run Blocked for Stewart (a package approval, a threshold) silently becomes Backlog: the Blocked count and the shift-end alarm never fire, and a lane claims it again.

## Acceptance criteria

- [ ] When `Invoke-Integrate` fails for a run whose state is `Blocked` or `Deferred`, the task reaches that same folder on the shared branch with its block reason, and the code commits are kept on the park branch with the park reason added to the task's `## Log`.
- [ ] A `-TestIntegrateLock` (or `-TestPark`) self-test case pins it: a Blocked run whose code verify is red ends with the task in `Blocked` on `origin/work`, the code not shared, and the park branch holding it.
- [ ] A Backlog run whose code verify is red is still parked to Backlog as now (the existing case keeps passing).
- [ ] The factory's own self-tests touched by the change pass under Windows PowerShell and `pwsh`.

## Notes

## Log

- 2026-10-31: Created.
