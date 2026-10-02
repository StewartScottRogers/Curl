---
id: BL-1183
title: Give audit findings deferred and blocked statuses with a reason and a log
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Audit/Findings/README.md, Audit/Findings/FINDING-TEMPLATE.md, Audit/Triage.md, Audit/Tools]
lane: no
requirement: none
created: 2026-10-02
completed:
---
# BL-1183 — Give audit findings deferred and blocked statuses with a reason and a log

## Goal

An audit finding can be `deferred` or `blocked` as well as `proposed`, `accepted`, `rejected` or `closed`, each move recorded with a reason and a date, so Stewart can park a finding before deciding it.

## Context

- Stewart, 2026-10-02: findings he must decide get their own kanban on the board page (BL-1184, BL-1185). Columns: Waiting for you (`proposed`), Approved (`accepted`), Deferred, Blocked; `rejected` is a folded list, not a column.
- His rules for the moves: from `proposed` to `accepted`, `deferred` or `blocked`; from `deferred` back to `proposed` ("cycle it back in") or to `rejected`; from `blocked` back to `proposed`. Rejecting happens only from `deferred`: "I may not understand what it does and need to defer it before making a decision." `accepted` → `closed` stays the re-audit's move ([closure rule](../../Audit/Findings/README.md#rules)).
- Only Stewart makes these moves (Triage step 2). Claude sets them only when Stewart names the finding and the move.
- Audit paths: this task is interactive only (`lane: no`); a lane must not take it. Do the work on the `audit` branch and merge its pull request under the standing exception once CI is green.
- Tools that read `status` today: `Audit/Tools/New-TasksFromAcceptedFindings.ps1`, `Write-AuditFindings.ps1`, `Write-AuditScorecard.ps1`, `Audit/RunAudit.ps1`. A deferred or blocked finding must not be filed as a task, must not be counted as open-and-undecided by anything that alarms on it, and must keep its ID across re-audits like a proposed one.

## Acceptance criteria

- [ ] `Audit/Findings/README.md`'s Statuses table lists `deferred` and `blocked` with their meaning and "Stewart" as who sets them, and a transitions table gives exactly the moves above.
- [ ] `FINDING-TEMPLATE.md` has a `reason:` front-matter field (the latest move's reason; empty for `proposed`) and a `## Log` section, last, append-only, one dated line per move, like a task's.
- [ ] The four existing findings (AF-0001 to AF-0004) gain an empty `reason:` and a `## Log` whose first line records their current status, without changing that status.
- [ ] `New-TasksFromAcceptedFindings.ps1 -WhatIf` files nothing for a `deferred` or `blocked` finding (checked with a scratch copy of one finding).
- [ ] `Write-AuditFindings.ps1` and `Write-AuditScorecard.ps1` keep a `deferred` or `blocked` finding's ID and status when the next audit re-raises it, as they do for `proposed`.
- [ ] `Audit/Triage.md` step 2 names the new statuses and the board page's Audit tab (BL-1185) as one way Stewart decides.
- [ ] The audit guard CI job passes and `dotnet build` is clean with the fast tests green.

## Notes

## Log

- 2026-10-02: Created.
