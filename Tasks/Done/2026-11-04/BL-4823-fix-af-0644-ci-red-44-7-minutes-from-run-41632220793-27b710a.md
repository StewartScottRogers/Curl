---
id: BL-4823
title: Fix AF-0644: CI red 44.7 minutes from run 41632220793 (27b710a0)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4823 — Fix AF-0644: CI red 44.7 minutes from run 41632220793 (27b710a0)

## Goal

The cause of the factory behaviour the audit office reported as AF-0644 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0644 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0644-ci-red-44-7-minutes-from-run-41632220793-27b710a0.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-11-03T12:19:28Z to 13:04:10Z, 44.7 min, runId 41632220793 on 27b710a0. Run 41634593982 (62178b13) also failed, followed by 15 cancelled runs until success 41639250979 (eae28957). This spell ended before the previous audit's window. -Since 2026-11-03 is a whole day, so it is counted again here. It is the incident of open finding AF-0614, whose task BL-4778 is Blocked ('Criterion 1 cannot hold as written', DarkFactory-20261103-204644.log:22:03:25). No merge to master happened during the spell.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-220320\logs -CiRunsJson Z:\repos\Curl.audit\20261103-220320\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41632220793
```

- Expected: No red spell over 30 minutes
- Actual: start 2026-11-03T12:19:28Z end 13:04:10Z minutes 44.7 runId 41632220793

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the window's start: the fix's landing, or for a task with no fix yet the later of its fix's landing and ADR-1235's commit), gives the finding's expected result with CI red time counted only while a run on the branch has a failing test or build error (ADR-1235): a failed run whose only non-success jobs were never given a runner, ran into their `timeout-minutes` in a job that tests nothing, or were superseded by a newer commit is left out, as `gh run view <id> --json jobs` and each job's annotations classify it. Notes name the fix, its landing time, the window, each run left out and why, and the numbers. If no factory shift has run since the window's start, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: AF-0644 is AF-0614's incident counted again (the audit's `-Since 2026-11-03` spans the whole day): run 41632220793 failed on known-failure lines 1df901115 had already dropped, and the next run, 41634593982, on a DoQ regression whose failed Linux job went unread while its run reported `queued`, with one filing push failing. BL-4778 holds the full analysis.
- Fixes: 73fa18c5a (BL-4647, 2026-11-03 10:47:28 MST) reads and cancels the running run while it reports `queued`; 5469c7711 (BL-4716/BL-4717, 2026-11-03 11:02:16 MST, 18:02:16Z) has the watch look every minute while CI is red, so a failed filing push is retried within a minute, and keeps the coordinator from cancelling a run that holds a fix. The window starts at the later, 18:02:16Z.
- Measurement under ADR-1235 (b6dd48c99; interactive session, 2026-11-04 05:10 MST): `powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since '2026-11-03 11:02:16' -LogRoot Z:\repos\Curl.logs -CiRunsJson <gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500, read 2026-11-04 05:05 MST>`, run on that list and on the same list without the runs left out below. Window 2026-11-03T18:02:16Z to 2026-11-04T12:05Z: shifts DarkFactory-20261103-080214 (idle poll), -134646 and -145254 (13 lanes), -162612, -165213, -183829, -185800, -195556, -203202, -204644 (idle poll 22:03 to 03:24 MST) and -20261104-032444 (8 lanes, 03:25 to 05:00 MST).
- Runs left out (jobs and annotations read 2026-11-04 with `gh run view <id> --json jobs` and `gh api repos/StewartScottRogers/Curl/check-runs/<job id>/annotations`): 41715208450 (2533dfc6), `Build and test (macos-latest)` cancelled ("The job was not acquired by Runner of type hosted even after multiple attempts"); 41723085402 (441bcff2), `Audit paths untouched by the dark factory` cancelled ("The job has exceeded the maximum execution time of 5m0s" in `actions/checkout`) and `Audit self-tests (macos-latest)` not acquired by a runner. No test failed and nothing failed to build in either. Runs kept: 41735630431 (fad49f8f) and 41764281440 (48505d3b), each failing only `Build and test (windows-latest)` step Fast tests.
- Numbers: raw, ciRedMinutes 102.22 (41715208450 71.8, 41735630431 16.15, 41764281440 14.27 min). Under ADR-1235, ciRedMinutes 30.42; ciRedSpells 41735630431 2026-11-03T23:19:11Z-23:35:20Z 16.15 min and 41764281440 2026-11-04T03:22:57Z-03:37:13Z 14.27 min: none over 30 minutes, none still red. tasksDone 80, tasksClaimedMoreThanOnce 0, requeues 0, unfinishedRuns 0. Criterion 1 holds.
- Build and fast tests (criterion 2), 2026-11-04 at aef2fa6d2: `dotnet build` 0 warnings, 0 errors; `dotnet test --filter "TestCategory!=Integration"` exit 0, no project failed.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 2ab4542b 9742157d.
- 2026-11-04: Doing -> Done. Under ADR-1235 the window from 5469c7711 (2026-11-03T18:02:16Z) holds spells of 16.15 and 14.27 min, none over 30.
