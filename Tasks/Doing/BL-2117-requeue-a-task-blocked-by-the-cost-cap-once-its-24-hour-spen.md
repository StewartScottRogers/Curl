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
completed:
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

- [ ] At shift start, and on each coordinator heartbeat, a Blocked task whose Blocked reason is the daily-rule refusal ("so it was not run again") moves to Backlog once `Test-TaskCapSpent` no longer holds for it. The move goes through `task-board.ps1 move`, with a reason naming the cost-cap window.
- [ ] A task blocked because a run stopped at the cap ("stopped at its ... cost cap") is never requeued this way.
- [ ] A `-TestTaskBudget` case, or a new self-test, proves both rules.
- [ ] The header help describes the requeue.

## Notes

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing.
