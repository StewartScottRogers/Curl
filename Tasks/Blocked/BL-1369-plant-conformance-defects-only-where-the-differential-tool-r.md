---
id: BL-1369
title: Plant conformance defects only where the differential tool reaches, and record each one's trigger
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-1367]
touches: [Audit/PlantedDefects, .claude/agents/audit-seeder.md]
lane: no
requirement: none
created: 2026-10-03
completed:
---
# BL-1369 — Plant conformance defects only where the differential tool reaches, and record each one's trigger

## Goal

Every planted conformance defect is reachable by the conformance auditor's generated cases, and the manifest says how to trigger it.

## Context

- Diagnosed 2026-10-03 across the audits of 2026-10-02 14:00 (run Z:\repos\Curl.audit\20261002-140002, planted commit 5232c4f8) and 2026-10-03 06:23 (run 20261003-062343, planted commit 5089bd0d): quality, truthfulness, process and conformance caught 0 of their planted defects. No auditor ran out of budget. Audit paths: interactive only, done on the `audit` branch and merged by pull request once CI is green.
- Run 2's PD-301 (Curl.Console/CurlCommandRunner.cs:446) is reached only by a malformed -T URL (`-T f "http://h/d ir/"`); the differential tool always appends http://127.0.0.1:PORT/ and excludes --url, so no generated case reaches it.

## Acceptance criteria

- [x] The conformance catalogue entries name only mappings the differential tool's value pools reach (for example connection refused 7, range 33, missing -T file 26), none needing a malformed URL.
- [x] audit-seeder.md step 2: for a conformance plant, run one command that shows the changed exit code in the planted build and record it in the manifest as `trigger`.
- [x] A seeded manifest's conformance entry has a `trigger` that reproduces the difference in the planted build (Notes record it).
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Blocked. Done on the audit branch; waiting on PR #72's CI before merging to master
