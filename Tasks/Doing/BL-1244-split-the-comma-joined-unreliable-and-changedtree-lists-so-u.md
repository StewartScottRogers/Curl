---
id: BL-1244
title: Split the comma-joined -Unreliable and -ChangedTree lists so unreliable auditors close nothing
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Tools/Write-AuditFindings.ps1, Audit/Tools/Write-AuditScorecard.ps1, Audit/Findings, Audit/Scorecards]
lane: no
requirement: none
created: 2026-10-02
completed:
---
# BL-1244 — Split the comma-joined -Unreliable and -ChangedTree lists so unreliable auditors close nothing

## Goal

An auditor flagged unreliable on an audit closes no finding and its new findings say so, whatever way RunAudit.ps1 passes the list of unreliable auditors.

## Context

- The 2026-10-02 14:00 audit (PR #50) logged "unreliable: quality, truthfulness, process", yet closed AF-0001, AF-0003 and AF-0004 on re-audits by truthfulness and process. RunAudit.ps1 passes `-Unreliable ($unreliable -join ',')` (and `-ChangedTree` the same way) through `powershell -File`, which binds the whole "quality,truthfulness,process" to one `[string[]]` element, so `$Unreliable -contains 'truthfulness'` was false (findings README rules 3 and 6).

## Acceptance criteria

- [x] Write-AuditFindings.ps1 splits a comma-joined -Unreliable, and Write-AuditScorecard.ps1 a comma-joined -ChangedTree, into names.
- [x] Write-AuditFindings.ps1 -SelfTest passes its unreliable list as one comma-joined string and still checks the unreliable auditor closes nothing; a new case checks the split.
- [x] The 14:00 audit's findings step and scorecard are re-run with the fix from the findings before it: AF-0001, AF-0003 and AF-0004 are accepted again, the seven findings from unreliable auditors carry rule 6's note, and only the scorecard's Closed and Still open cells for truthfulness and process change.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-02: Fixed in 69919d2b on the `audit` branch, inside the audit's own PR #50, merged once CI passed on Windows, Linux and macOS. Re-run: "findings: new 21, still open 0, closed 0, catches 3" (the broken run said closed 3).
## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
