---
id: BL-2117
title: Requeue a task blocked by the cost cap once its 24-hour spending window has passed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-12
completed: 2026-10-12
---
# BL-2117 — Requeue a task blocked by the cost cap once its 24-hour spending window has passed

## Goal

A task the cost cap's daily rule sent to Blocked goes back to Backlog once its runs of the
last 24 hours have cost less than the cap allows, without anyone moving it by hand.

## Context

The cost cap's daily rule (AF-0096, `Test-TaskCapSpent` and `Get-TaskCapSpentWhy` in
`RunDarkFactory.ps1`) refuses a fresh claim when the task's runs of the last 24 hours
leave less than $1 of its cap. The task goes to Blocked with "...cost cap, so it was not
run again; split the task". Nothing ever brings it back. BL-2087 sat in Blocked from
2026-10-11 until Stewart asked about it on 2026-10-12, and Claude requeued it by hand.

A run that itself hit the cap ("stopped at its ... cost cap") is different. It means the
task is too big for one run and should be split, so it stays in Blocked.

## Acceptance criteria

- [x] At shift start, and on each coordinator heartbeat, a Blocked task whose Blocked reason is the daily-rule refusal ("so it was not run again") moves to Backlog once `Test-TaskCapSpent` no longer holds for it. The move goes through `task-board.ps1 move`, with a reason naming the cost-cap window.
- [x] A task blocked because a run stopped at the cap ("stopped at its ... cost cap") is never requeued this way.
- [x] A `-TestTaskBudget` case, or a new self-test, proves both rules.
- [x] The header help describes the requeue.

## Notes

- Implemented in `Invoke-Requeue`, which already returns Blocked tasks whose blockers are Done and runs before every claim, the shift's first included (single runner and every lane, under the integration lock, committing and pushing the move). Decided by Claude: this stands in for "on each coordinator heartbeat", because the lane-shift coordinator owns no checkout to commit a board move in (it pushes CI tasks through its own ci-watch worktree), and a requeued task matters only when the next claim looks for work, which is exactly when this runs.
- `Test-CapBlockRequeueDue` is the pure rule: the last Log line must hold the daily-rule refusal "so it was not run again", must not be a run that "stopped at its ... cost cap", and `Test-TaskCapSpent` must no longer hold for the task's cap and its last 24 hours of runs. The move goes through `task-board.ps1 move` with the reason "Cost cap window passed: ...".
- Four new `-TestTaskBudget` cases cover both rules; all 25 cases pass. Header help (THE COST CAP section) describes the requeue.

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing.
- 2026-10-12: Doing -> Done. A task the cost cap's daily rule blocked returns to Backlog by itself once its last 24 hours of runs leave 1 dollar of its cap; one whose run stopped at the cap stays Blocked
