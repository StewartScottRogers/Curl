---
id: BL-1367
title: Make the planted CI-red defect's catch text name its own run
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/PlantedDefects, .claude/agents/audit-seeder.md]
lane: no
requirement: none
created: 2026-10-03
completed: 2026-10-04
---
# BL-1367 — Make the planted CI-red defect's catch text name its own run

## Goal

The planted CI-red defect (PD-502) is caught only by a finding about the planted spell, not by any CI-red finding.

## Context

- Diagnosed 2026-10-03 across the audits of 2026-10-02 14:00 (run Z:\repos\Curl.audit\20261002-140002, planted commit 5232c4f8) and 2026-10-03 06:23 (run 20261003-062343, planted commit 5089bd0d): quality, truthfulness, process and conformance caught 0 of their planted defects. No auditor ran out of budget. Audit paths: interactive only, done on the `audit` branch and merged by pull request once CI is green.
- PD-502's catch text is `ci-red`, which matches any CI-red finding, while real red time already crossed the threshold in both runs.

## Acceptance criteria

- [x] The PD-502 catalogue entry tells the seeder to write the planted run's id, or its end time, into the manifest's `catch`.
- [x] audit-seeder.md says so in its PD-502 step.
- [x] A seeding into a scratch copy writes a PD-502 catch naming the planted run (Notes record it).
- [x] `dotnet build` is clean and the fast tests are green.

## Notes
- 2026-10-04: Merged in PR #61. The PD-502 catalogue entry (re-encoded from a decoded copy outside the repository) has the seeder give the planted failed run a databaseId no real run shares and use it as the catch; audit-seeder.md says a finding kind is never a catch alone where the real tree produces it. Not seeded by the agent itself: simulated by adding such a run to the 2026-10-03 06:23 log copy's ci-runs.json, where Measure-FactoryProcess reported exactly one 90-minute spell with runId 90000763536. The next audit's seeding confirms it.

## Log

- 2026-10-03: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Blocked. On the audit branch (PR #61); waits for CI and the merge. An interactive session completes it.
- 2026-10-04: Blocked -> Doing.
- 2026-10-04: Doing -> Done. Merged in PR #61
