---
id: BL-2116
title: Raise the dark factory run cost cap to 4 times the median, floor 3 USD, ceiling 10 USD
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [RunDarkFactory.ps1, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-12
completed:
---
# BL-2116 — Raise the dark factory run cost cap to 4 times the median, floor 3 USD, ceiling 10 USD

## Goal

Each headless task run is capped at 4 times the median recent run cost, never below $3 and
never above $10 (`-TaskBudgetUsd` default), instead of 2.7 times, $2 and $6.

## Context

Stewart asked on 2026-10-12 to raise the cost cap. Claude picked the numbers under the
delegation. At today's median of about $0.75 the cap goes from about $2.10 to $3.00, so a
task that needs a few more turns finishes instead of going to Blocked. The daily rule
(AF-0095, AF-0096: a task's runs of the last 24 hours count against one cap, and a claim
with less than $1 left is refused) stays as it is.

Today the cap is 2.7 times the median, never below $2 and never above `-TaskBudgetUsd`
(default 6); see ADR-0407. The places to change are:
- `RunDarkFactory.ps1`'s `Get-RunBudgetUsd`, around line 1918;
- the header help (around lines 48-66);
- the parameter comment (around line 360);
- the -TestTaskBudget cases (around line 2149);
- the Blocked reason text (around line 5226);
- the comment around line 3709.

The process auditor still names a task run costing over three times the median (AF-0033,
AF-0172). After this change that is a deliberate margin, not a defect. Stewart can reject
such findings, or a later audit-branch change can move the auditor's bar.

## Acceptance criteria

- [ ] `Get-RunBudgetUsd` uses 4.0 times the median, a $3 floor, and `-TaskBudgetUsd` defaults to 10.
- [ ] Every comment, the header help and the Blocked reason say 4 times, $3 and $10. No "2.7" about the cap is left in `RunDarkFactory.ps1`.
- [ ] `RunDarkFactory.ps1 -TestTaskBudget` passes, with its cases updated to the new numbers (for example, a median of 1.3 gives a cap of 5.2).
- [ ] A new ADR, "Decided by Claude under Stewart's delegation", supersedes ADR-0407's numbers and says why. ADR-0407's status names it.

## Notes

## Log

- 2026-10-12: Created.
