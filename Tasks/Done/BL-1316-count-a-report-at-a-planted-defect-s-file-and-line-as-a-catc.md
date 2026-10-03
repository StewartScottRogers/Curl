---
id: BL-1316
title: Count a report at a planted defect's file and line as a catch, whatever its wording
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Tools/Write-AuditFindings.ps1, Audit/Tools/Write-AuditScorecard.ps1, Audit/Tools/Fixtures]
lane: no
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1316 — Count a report at a planted defect's file and line as a catch, whatever its wording

## Goal

A report from a planted defect's own auditor, in its file and within 2 lines of its planted line, is a catch whatever its wording, so one planted defect reported for two symptoms never becomes a finding.

## Context

- The 2026-10-02 14:00 audit: the performance auditor reported planted defect PD-202 (`HttpResponseBodyReader.cs:294`, catch text `new byte[1]`) twice. The "334x slower" report named the catch text and counted; the "3.7x memory" report said "a new 1-byte array" and was filed as AF-0018, whose task BL-1274 found its cause was not in the code.

## Acceptance criteria

- [x] `Write-AuditFindings.ps1`'s `Test-Catch` and `Write-AuditScorecard.ps1`'s `Test-Caught` accept a location within 2 lines of the manifest's `line`, beside the catch text.
- [x] The findings self-test has a differently worded report at the planted line that is a catch and not filed, and checks the window's edges (42 yes, 43 no, 30-38 yes, 30-37 no, no line no).
- [x] `Audit/Scorecards/README.md`'s "Planted caught" says the same.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-03: PR #53, merged as 471e90fc once CI passed on Windows, Linux and macOS for e540ebcb. The self-test also needed CRLF-tolerant checks and unrolling the JSON array Windows PowerShell 5.1 reads back as one object. Also changed: `Audit/Scorecards/README.md` (an audit path, so no lane could collide). AF-0018 stays accepted until a reliable performance re-audit closes it.
## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Blocked. Waiting on PR #53 (audit branch) CI; an interactive session merges it and completes this task. Parked so the next shift can start.
- 2026-10-03: Blocked -> Doing.
- 2026-10-03: Doing -> Done. A report at a planted line is a catch; merged in PR #53
