---
id: BL-1366
title: Report unfinished runs, CI-red spells and lane minutes from Measure-FactoryProcess, and file every crossed process rule
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Tools/Measure-FactoryProcess.ps1, Audit/Instructions/Process.md]
lane: no
requirement: none
created: 2026-10-03
completed:
---
# BL-1366 — Report unfinished runs, CI-red spells and lane minutes from Measure-FactoryProcess, and file every crossed process rule

## Goal

The process auditor sees every rule's measure and files a finding for each rule crossed, with a location naming the log file.

## Context

- Diagnosed 2026-10-03 across the audits of 2026-10-02 14:00 (run Z:\repos\Curl.audit\20261002-140002, planted commit 5232c4f8) and 2026-10-03 06:23 (run 20261003-062343, planted commit 5089bd0d): quality, truthfulness, process and conformance caught 0 of their planted defects. No auditor ran out of budget. Audit paths: interactive only, done on the `audit` branch and merged by pull request once CI is green.
- Measure-FactoryProcess.ps1 works out HasResult per run but never reports unfinished runs, so PD-503 (BL-1129-20261001-150000-L9.jsonl) was invisible. Run 2's own metrics showed ciRedMinutes 364.88 against a 60-minute threshold (run 1: 299) and no ci-red finding was filed; overlap waits were 10,259 of 10,326 idle minutes.

## Acceptance criteria

- [ ] Measure-FactoryProcess.ps1 outputs `unfinishedRuns` (run-log file names with no result event and no FACTORY: DONE or BLOCKED), `ciRedSpells` (start, end, minutes and run id of each red spell) and `laneMinutes`, and its -SelfTest covers each.
- [ ] Process.md gives every rule a crossed or not-crossed line in the summary and a finding whenever it is crossed, with the location naming the log file as `logs/<file>`.
- [ ] On run 1's log copy `unfinishedRuns` lists BL-1129-20261001-150000-L9.jsonl, and on run 2's `ciRedSpells` shows a spell of about 90 minutes ending 04:21:06Z (Notes record both).
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Blocked. Paused for BL-1360, which an interactive session is finishing; resumes after.
- 2026-10-03: Blocked -> Backlog. Unblocked: BL-1360 now Done
- 2026-10-03: Backlog -> Doing.
