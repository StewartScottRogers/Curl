---
id: BL-1370
title: Add a boolean-name scan to the truthfulness auditor's method
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-1364, BL-1365]
touches: [Audit/Instructions/Truthfulness.md, Audit/Tools/Write-AuditFindings.ps1, Audit/Tools/Write-AuditScorecard.ps1]
lane: no
requirement: none
created: 2026-10-03
completed:
---
# BL-1370 — Add a boolean-name scan to the truthfulness auditor's method

## Goal

The truthfulness auditor checks every public bool member named Is, Has, Lacks, Can or Not against its returns doc, and an ADR cited by its file name matches its planted path.

## Context

- Diagnosed 2026-10-03 across the audits of 2026-10-02 14:00 (run Z:\repos\Curl.audit\20261002-140002, planted commit 5232c4f8) and 2026-10-03 06:23 (run 20261003-062343, planted commit 5089bd0d): quality, truthfulness, process and conformance caught 0 of their planted defects. No auditor ran out of budget. Audit paths: interactive only, done on the `audit` branch and merged by pull request once CI is green.
- Run 1's PD-401 (Curl.Core.UnitLibrary/UrlSchemeGuesser.cs:70: LacksScheme returns true "when the URL names its own scheme") would rarely be among 30 randomly sampled methods.

## Acceptance criteria

- [ ] Truthfulness.md step 1 adds a scan of every public bool method or property whose name starts Is, Has, Lacks, Can or Not, compared with its <returns> doc, with the PowerShell command written out.
- [ ] Test-SamePath in both tools also accepts a location that is the planted file's base name (an ADR cited by its file name), with self-test cases.
- [ ] A truthfulness-only rerun on the run 1 planted tree catches PD-401 (Notes record it).
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Blocked. On the audit branch (PR #62); waits for CI and the merge. An interactive session completes it.
