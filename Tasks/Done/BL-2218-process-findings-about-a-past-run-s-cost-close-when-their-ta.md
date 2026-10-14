---
id: BL-2218
title: Process findings about a past run's cost close when their task is Done instead of being re-fixed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Audit, .claude/agents]
lane: no
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2218 — Process findings about a past run's cost close when their task is Done instead of being re-fixed

## Goal

A process finding about one past run's cost (key `process:logs:<task>:cost-outlier`) closes
when every task it names is Done, instead of being re-audited forever and re-filed as a
"Re-fix" task.

## Context

Stewart, 2026-10-14: "change it". A cost finding such as AF-0204 ("BL-2142 cost $3.11, 4.25
times the median") describes a run that already happened. History cannot change, so every
re-audit says it still reproduces. `Audit/Tools/New-TasksFromAcceptedFindings.ps1` then files
a Re-fix task each time (BL-2205 to BL-2208 for AF-0190, AF-0191, AF-0204, AF-0205), and the
lane can only record that nothing can be fixed. ADR-0422 says findings close only on evidence,
never because a task is Done. For a cost finding about a fixed past run, the task is the only
evidence there can be: its job is to look for avoidable waste and fix the cause, or record that
there is none.

Where to look:
- ADR-0422, which needs an amendment.
- `Audit/Tools/New-TasksFromAcceptedFindings.ps1`: its reopen rule files the Re-fix tasks.
- `Audit/Tools/Write-AuditFindings.ps1` and `RunAudit.ps1`: where closures are decided and
  re-audit verdicts are recorded.
- `Audit/Instructions/Process.md` and `.claude/agents/audit-process.md`: the cost-outlier
  method and its key.
- CLAUDE.md's "Audit office" section, which states the closing rule.

Audit paths go through the `audit` branch: merge, never rebase. Land the change through an
audit -> master pull request merged once CI is green.

## Acceptance criteria

- [x] A finding whose key ends in `:cost-outlier` closes, with `closed-how` naming the rule (for example `task-done-historical`) and a reason, at the first audit after all its tasks are Done. It is not re-audited and no Re-fix task is filed for it.
- [x] Every other finding still follows ADR-0422 unchanged, and the self-tests of `New-TasksFromAcceptedFindings.ps1` and `Write-AuditFindings.ps1` prove both cases.
- [x] AF-0190, AF-0191, AF-0204 and AF-0205 are closed under the new rule (their original tasks are Done). Re-fix tasks BL-2205 to BL-2208 are moved to Done on work/dark-factory with that reason, unless a lane already finished them.
- [x] ADR-0422 carries an amendment note, and CLAUDE.md's closing-rule paragraph and the process auditor's method say the same.
- [x] The change lands through an audit-branch pull request with CI green on Windows, Linux and macOS.

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing. interactive session
- 2026-10-14: Doing -> Done. PR #141 merged: cost-outlier findings close when their tasks are Done
