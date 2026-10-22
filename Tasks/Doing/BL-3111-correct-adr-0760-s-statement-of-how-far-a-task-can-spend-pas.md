---
id: BL-3111
title: Correct ADR-0760's statement of how far a task can spend past its cost cap
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0760-a-run-starts-only-with-2-dollars-of-its-cap-left.md]
requirement: none
created: 2026-10-22
completed:
---
# BL-3111 — Correct ADR-0760's statement of how far a task can spend past its cost cap

## Goal

ADR-0760's Consequences state correctly how far past its cost cap a task can spend in a day.

## Context

Found by the shift-end review of 2026-10-22 (code-reviewer, ADR-0710) in the diff 67f6ff17b...3894d9c9b. CLAUDE.md "say what it does": a document that states something false of the code is a defect.

- `Documentation/Planning/Decisions/ADR-0760-a-run-starts-only-with-2-dollars-of-its-cap-left.md`, last Consequences bullet, says "A task can spend up to $2 more than its cap in a day (cap less $2.01 spent, then a $2 run)".
- With $2.01 left, `Test-TaskCapSpent` (`DarkFactory/TaskBudget.ps1:183-189`) returns false and `Get-RunBudgetUsd` gives `cap - spent` = $2.01, so the total is the cap, not $2 over. The $2 floor only exceeds what is left for a run `Test-TaskCapSpent` does not gate.

## Acceptance criteria

- [x] The bullet states the real bound as the code gives it (a fresh claim never starts with less than $2 left, so the floor does not overshoot for it; name any path that can overshoot, and that a run can overrun its own cap by part of a turn), and drops the "$2.01 spent, then a $2 run" example.
- [x] Every number in the bullet can be reproduced from `Get-RunBudgetUsd` and `Test-TaskCapSpent` in `DarkFactory/TaskBudget.ps1`.

## Notes

Checked against TaskBudget.ps1 (Get-RunBudgetUsd, Test-TaskCapSpent) and ShiftRunner.ps1:131 (resumed and overtime runs skip the gate). Documentation-only change; no build or tests needed.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b25bcd25 9f77ca2a.
