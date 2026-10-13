# ADR-0478 — The run cost cap is 4 times the median run, between $3 and $10

- **Status:** Accepted
- **Date:** 2026-10-12
- **Supersedes:** the numbers of ADR-0407 (2.7 times the median, $2 floor, `-TaskBudgetUsd` default 6)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-2116.

## Context

Stewart asked on 2026-10-12 to raise the cost cap and left the numbers to Claude. Under
ADR-0407 each headless run was capped at 2.7 times the median of the newest 40 task runs,
never below $2 and never above `-TaskBudgetUsd` (default 6). The median had fallen to
about $0.75, so the cap sat near $2.10: a task that needed a few more turns than most
reached the cap and went to Blocked, and its next claim spent part of what was left of
the same cap (AF-0095, AF-0096), so work was stashed and re-run instead of finished.

## Decision

1. `Get-RunBudgetUsd` in `RunDarkFactory.ps1` caps each run at 4 times the median of the
   newest 40 task runs' costs, read as ADR-0407 says.
2. The cap never drops below $3, so a run of cheap tasks cannot starve a larger one.
3. `-TaskBudgetUsd` defaults to 10 and stays the ceiling (0 still means no cap); with fewer
   than 10 logged runs the cap is `-TaskBudgetUsd` itself.
4. The daily rule is unchanged: what a task's runs of the last 24 hours cost comes off its
   next cap, down to $1, and a claim with less than $1 left goes to Blocked.

## Consequences

- At a median of $0.75 the cap is $3.00 instead of about $2.10; at a median of $1.30 it is
  $5.20.
- A run can now end above three times the median, the bar the process auditor measures
  (AF-0033, AF-0172). That is a deliberate margin, not a defect: Stewart may reject such
  findings, or a later change on the `audit` branch may move the auditor's bar.
- Fewer tasks go to Blocked for the cap; the ones that still do are truly too big for one run.
- `-TestTaskBudget` proves the new arithmetic.

## Alternatives considered

- **3 times the median, floor $2.50.** Lost: at today's median it raises the cap by
  about 40 cents, too little to change which tasks finish.
- **Remove the cap (`-TaskBudgetUsd 0`).** Lost: a run caught in a loop would spend
  without bound; AF-0004 is why the cap exists.
