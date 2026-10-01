# ADR-0288 — Dark factory runs stop at a $6 cost cap and block the task

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1070.

## Context

Audit finding AF-0004 (process auditor, accepted by Stewart) found 31 tasks that cost
more than three times the median task. The median was about $2.36, so the bar was about
$7.08; the costliest were BL-703 ($14.33), BL-527 ($13.83), BL-568 ($12.95), BL-658
($12.58) and BL-708 ($11.93). `RunDarkFactory.ps1` limited a run only by time
(`-TaskMinutes`, 120 minutes), which a run can spend at any cost.

`claude -p` has `--max-budget-usd <amount>`. Measured on 2026-10-01 with a $0.0001 cap,
a run that reaches it ends with a `result` event whose `subtype` is
`error_max_budget_usd`, `terminal_reason` is `budget_exhausted`, `is_error` is true and
`errors` is `["Reached maximum budget ($0.0001)"]`. The cap is checked between turns,
so the run ended at $0.057: one turn's cost above it.

## Decision

1. `RunDarkFactory.ps1` takes `-TaskBudgetUsd` (default 6, 0 for no cap) and passes it
   as `--max-budget-usd` to every headless run `Invoke-TaskRun` starts, and forwards it
   to lanes and to the next `-Continuous` shift.
2. $6 rather than $7: a run can end one turn above the cap, and a turn late in a long
   run, with a large context, costs tens of cents, so $6 keeps a capped run under three
   times the median the audit measured.
3. A task run that reaches the cap (`Test-BudgetSpent`: the result's subtype is
   `error_max_budget_usd`) and leaves its task in `Doing` is treated like a timed-out
   run: its partial work is stashed and the task goes to `Blocked` with a reason that
   starts `Stewart:` and says it stopped at the cost cap and wants splitting. It is not
   requeued, since a rerun would spend the same again, and it is not treated as an API
   failure (`Test-ApiFailure` looks for `api_error_status` or `terminal_reason`
   `api_error`, which a budget stop has neither of).

## Consequences

- No single run costs more than about $6 plus one turn. A task run cut off by the usage
  limit and resumed gets a fresh cap for the resumed run, so such a task can cost more
  across its runs; that is rare and its cost was spent on real work.
- The rebase resolver run is under the same cap; one that reaches it leaves the rebase
  unsettled, and the lane parks the task as for any unsettled conflict.
- Tasks blocked at the cap are a signal to split them; the board sends them to Stewart
  with the run's log path.

## Alternatives considered

- **Requeue to Backlog at the cap.** Lost: the next run starts from the same task and
  spends the same again, with no cap on the total.
- **Resume the run with a second budget.** Lost: it doubles the cap instead of enforcing
  it, which is the cost AF-0004 complains of.
- **Cap by turns (`--max-turns`).** Lost: turn cost varies with context size by an order
  of magnitude, and the audit measures dollars.
