# ADR-0760 — A run starts only with $2 of its cost cap left

- **Status:** Accepted
- **Date:** 2026-10-22
- **Amends:** the $1 floor of AF-0095 and AF-0096 in `DarkFactory/TaskBudget.ps1` (ADR-0478 keeps the cap itself)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-3079.

## Context

What a task's runs of the last 24 hours cost comes off its next run's cap, and a fresh
claim is refused once less than $1 of the cap is left. AF-0326 found BL-2970 claimed three
times: after its first claim spent $4.27, its second claim ran on $1.02 of cap (13 turns,
$0.87) and its third on what was left (5 turns, $0.46). Each requeued for the cost cap with
nothing landed. A Curl task run reads the lane rules, the task and its files before its
first edit, which alone costs about $0.50 to $1; a run starting with barely $1 can only
measure and requeue.

## Decision

`$MinRunBudgetUsd = 2.0` in `DarkFactory/TaskBudget.ps1`. `Get-RunBudgetUsd` never gives a
run less than $2, and `Test-TaskCapSpent` refuses a fresh claim (the task goes to Blocked
with "so it was not run again", where ADR-0759 splits it) once the task's runs of the last
day leave less than $2 of its cap. `Test-CapBlockRequeueDue` and `Get-CapResetTime` follow
through `Test-TaskCapSpent`. `-TestTaskBudget` pins BL-2970's numbers ($4.27 of a $5.29 cap
is spent).

## Consequences

- A task that spent most of its cap goes to the split at once instead of through one or two
  short claims that only requeue.
- A fresh claim never overshoots its cap: `Test-TaskCapSpent` refuses it once less than $2
  of the cap is left, so a claim that starts has at least $2 left and `Get-RunBudgetUsd`
  gives it `cap - spent`, which the $2 floor does not raise. Spending the cap exactly is the
  most a fresh claim's runs add up to, plus the part of a turn a run overruns its own
  `--max-budget-usd` by (the cap is checked between turns).
- Only a run that skips that gate can overshoot: a resumed run and an overtime run
  (`ShiftRunner.ps1` passes them without `Get-TaskCapSpentWhy`) get the $2 floor however
  little is left, so such a task can spend up to $2 more than its cap in a day (cap fully
  spent, then a $2 run), against $1 before.
