---
id: BL-1365
title: Credit a process auditor's catch of a planted log defect by its catch text, not its log file name
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Tools/Write-AuditFindings.ps1, Audit/Tools/Write-AuditScorecard.ps1, Audit/Tools/Fixtures]
lane: no
requirement: none
created: 2026-10-03
completed:
---
# BL-1365 — Credit a process auditor's catch of a planted log defect by its catch text, not its log file name

## Goal

A process finding that names a planted log defect's catch text is a catch, whatever log file its location names.

## Context

- Diagnosed 2026-10-03 across the audits of 2026-10-02 14:00 (run Z:\repos\Curl.audit\20261002-140002, planted commit 5232c4f8) and 2026-10-03 06:23 (run 20261003-062343, planted commit 5089bd0d): quality, truthfulness, process and conformance caught 0 of their planted defects. No auditor ran out of budget. Audit paths: interactive only, done on the `audit` branch and merged by pull request once CI is green.
- Run 1's PD-501 (DarkFactory-20261001-120001-L9.log): the auditor reported key `process:logs:BL-1121:redone-work`, location `logs/BL-1121`, "BL-1121 claimed 5 times"; the catch text matched, but Test-SamePath needs the location to end in the planted file name. Run 2's PD-501: "BL-1289 was claimed 6 times" at `logs/BL-1289-20261002-211047-L1.jsonl:1`, the wrong file. A finding about a log defect rarely names the one file the seeder edited.

## Acceptance criteria

- [ ] Test-Catch (Write-AuditFindings.ps1) and Test-Caught (Write-AuditScorecard.ps1): when the planted defect's auditor is `process` and its file is in the log copy (no project folder: a .log, .jsonl or ci-runs.json), the same-file test is skipped and only the catch text in key, title or evidence is required.
- [ ] Both self-tests gain the two reports above as cases that count as catches, and a process report without the catch text that does not.
- [ ] Replaying run 1's and run 2's reports through both tools turns both PD-501s into catches (Notes record it).
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
