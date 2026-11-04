---
id: BL-4825
title: Count CI red time in Measure-FactoryProcess.ps1 only while a test or build fails (ADR-1235)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Tools/Measure-FactoryProcess.ps1, Audit/Tools/Fixtures/process]
lane: no
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4825 — Count CI red time in Measure-FactoryProcess.ps1 only while a test or build fails (ADR-1235)

## Goal

`Audit/Tools/Measure-FactoryProcess.ps1`'s `ciRedMinutes` and `ciRedSpells` count a failed CI run as red only when one of its jobs has a failing test or a build error, as ADR-1235 says, so the process auditor and the process tasks read the same number.

## Context

ADR-1235 (Stewart approved 2026-11-04) leaves out of the factory's CI red time any failed run whose only non-success jobs were cancelled because GitHub never assigned a runner ("The job was not acquired by Runner of type hosted even after multiple attempts"), or ran into their `timeout-minutes` in a job that tests nothing (a checkout or other set-up step; a test step that hangs into the timeout still counts), or were superseded by a newer commit. `Get-CiRedSpells` today counts every run whose conclusion is `failure`, so the runs 41688300433, 41715208450 and 41723085402 of 2026-11-03 made spells of 122.97 and 71.8 minutes with no failing test, and seven process tasks went to Blocked over them (BL-4647 and others, settled on 2026-11-04 by applying the rule by hand).

Where to start: `Get-CiRedSpells` in `Audit/Tools/Measure-FactoryProcess.ps1`; `Audit/RunAudit.ps1` saves `ci-runs.json`, which has only the run list. The tool needs each failed run's jobs (`gh run view <id> --json jobs`: a job's `conclusion` and its steps) and, for a cancelled job, its annotations (`gh api repos/StewartScottRogers/Curl/check-runs/<job id>/annotations`) to tell infrastructure from a failing test, read when gh is called and from a saved file when `-CiRunsJson` is given (for example a `ci-jobs.json` beside it, written by `RunAudit.ps1`). This is an audit path, so only an interactive session on the `audit` branch changes it (ADR-0267).

## Acceptance criteria

- [x] `Get-CiRedSpells` leaves out a failed run whose non-success jobs are all infrastructure under ADR-1235 (runner not acquired; timeout in a job with no failing test step; superseded), and still counts a run with a failed test or build step; the script's help says so.
- [x] `powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -SelfTest` passes under Windows PowerShell 5.1 and pwsh 7, with new fixture runs: one whose only failure is a macOS job not acquired by a runner, one whose audit-guard checkout ran past its timeout, and one with a failing test step, and the expected `ciRedMinutes` counts only the last.
- [x] The tool run with `-Since '2026-11-03 10:47:28'` over `Z:\repos\Curl.logs` and a run list read after 2026-11-04 05:05 MST gives no spell starting at 41688300433 or 41715208450, and its spells ending before 2026-11-04T12:05Z sum to 30.42 minutes, as BL-4647's Notes give.

## Notes

- Filed by the interactive session that wrote ADR-1235 (2026-11-04); that session changes no audit path.
- Done on the `audit` branch in commit 4f66756710821e75c21f1eff5762713ada82e8c3 (PR #303, audit -> master, not merged). `Get-CiRedSpells` drops a failed run whose non-success jobs are all infrastructure (runner not acquired; superseded; timeout with no unsuccessful test/build step) and lists it in the new `ciInfrastructureRuns`; jobs come from `-CiJobsJson`, a `ci-jobs.json` beside `-CiRunsJson`, or gh. The help says so.
- Self-tests: `Measure-FactoryProcess.ps1 -SelfTest` passes under Windows PowerShell 5.1 and pwsh 7 (24 PASS lines; new fixture runs 9 macOS runner not acquired, 10 audit-guard checkout timeout, 11 failing `Fast tests`; `ciRedMinutes` 150 = the old 120 + run 11's 30). All 13 Audit `-SelfTest` scripts pass under 5.1.
- Live check (2026-11-04 05:44 MST run list, `-Since '2026-11-03 10:47:28'`, `-LogRoot Z:\repos\Curl.logs`): spells 41735630431 (06:19:11Z-06:35:20Z, 16.15) and 41764281440 (10:22:57Z-10:37:13Z, 14.27) = 30.42 minutes before 12:05Z; 41688300433, 41715208450 and 41723085402 are in `ciInfrastructureRuns`, none starts a spell.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 927bd5a6 90e0df30 2e17a833.
- 2026-11-04: Doing -> Done. Measure-FactoryProcess.ps1 counts CI red time only while a test or build fails (audit 4f6675671, PR #303)
