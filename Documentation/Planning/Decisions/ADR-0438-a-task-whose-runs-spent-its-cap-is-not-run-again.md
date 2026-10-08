# ADR-0438 — A task whose runs of the last day spent its cost cap is not run again

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1774.

## Context

Audit finding AF-0096 (process auditor, accepted by Stewart) measured BL-1609 at $5.17,
4.3 times the median, over five claims: $0.41, $1.15, $2.03, $0.41 and $1.18, four of
them ending in a requeue. Each run stayed under its own cap. ADR-0437 takes a task's
runs of the last 24 hours off its next run's cap, but never below $1, so a task requeued
again and again still got $1 and one turn more each time, and its total had no bound.

## Decision

1. When a lane or the single runner freshly claims a task, `RunDarkFactory.ps1` compares
   what the task's runs of the last 24 hours cost (`Get-TaskSpentUsd`) with the run's
   cap before those runs come off (`Get-RunBudgetUsd`). When less than $1 of the cap is
   left (`Test-TaskCapSpent`), the task is not run: it goes to Blocked for Stewart with
   the reason "its runs of the last day already cost $x of its $y US dollar cost cap,
   so it was not run again; split the task", like a run that reaches its cap.
2. A resumed run (after the usage limit) and an overtime run are continuations of a run
   already going, so they are not checked.
3. A task blocked this way does not count towards the shift's failing-runs streak: it
   was never run.
4. `-TaskBudgetUsd 0` still means no cap and never blocks. `-TestTaskBudget` covers it.

## Why

- $1 matches ADR-0437's floor: any claim that would have been given only the floor is
  the claim that lets the total climb past one cap.
- Blocked, not Backlog: a task that spent a whole cap across several claims is too big
  for one run and wants splitting, which is Stewart's or a planning session's call.

## Consequences

- With BL-1609's costs and its $3.21 cap, the fourth claim would have been blocked after
  $3.59; the auditor's 3.57 line is three times the median, so a task's total is now at
  most its cap plus one turn.
- AF-0096's reproduction over 2026-10-07 still shows BL-1609's historical $5.17; logs do
  not change. The re-audit should measure runs after this change.
