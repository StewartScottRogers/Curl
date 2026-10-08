# ADR-0437 — A task's earlier runs of the last day come off its next run's cost cap

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1773.

## Context

Audit finding AF-0095 (process auditor, accepted by Stewart) measured BL-1488 at $6.56,
5.5 times the $1.19 median: lane 7's run spent $5.73, most of it on five Sonnet
subagents running in parallel, before the session budget ran out and the task was
requeued; lane 2 then finished it for $0.83. The parallel subagents were already fixed
by BL-1679 (AF-0052), which landed after that run and allows one subagent at a time.
What remained is that ADR-0407's cap is per run while the auditor measures per task, so
a task requeued and run again could cost close to two caps.

## Decision

1. Before each headless run, `RunDarkFactory.ps1` sums `total_cost_usd` over the task's
   own task and resumed run logs (`BL-n-<stamp>[-Ln][-resumed].jsonl`) written in the last
   24 hours (`Get-TaskSpentUsd`), and takes that off the run's cap (`Get-RunBudgetUsd
   -SpentUsd`).
2. The reduced cap never goes below $1, so a requeued run can still finish a nearly done
   task or hand it back cleanly.
3. `-TaskBudgetUsd 0` still means no cap. `-TestTaskBudget` proves the arithmetic.

## Why

- The last 24 hours rather than all history: a task parked days ago for an overlap or a
  dependency should start afresh, not with a spent cap; a requeue within one shift is the
  case AF-0095 found.
- $1 rather than the $2 floor of a fresh run: it keeps a twice-claimed task's total near
  one cap. A run that reaches it is Blocked like any other capped run, which is right for
  a task that spent its whole budget across two claims.

## Consequences

- AF-0095's reproduction over 2026-10-07 still shows BL-1488's historical $6.56; logs do
  not change. Runs after this change cannot add up past one cap plus $1 and one turn.
