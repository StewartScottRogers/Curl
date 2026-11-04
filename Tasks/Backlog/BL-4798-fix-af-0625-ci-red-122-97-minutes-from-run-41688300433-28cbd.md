---
id: BL-4798
title: Fix AF-0625: CI red 122.97 minutes from run 41688300433 (28cbd2df): failure log unreadable and no shift watching CI
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-03
completed:
---
# BL-4798 — Fix AF-0625: CI red 122.97 minutes from run 41688300433 (28cbd2df): failure log unreadable and no shift watching CI

## Goal

The cause of the factory behaviour the audit office reported as AF-0625 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0625 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0625-ci-red-122-97-minutes-from-run-41688300433-28cbd2d.md`.

Location: `logs/DarkFactory-20261103-134646.log:17`

Location: `logs/DarkFactory-20261103-134646.log:17`

ciRedSpells: 2026-11-03T18:20:55Z to 20:23:53Z (11:20 to 13:23 Arizona), 122.97 min, runId 41688300433 on 28cbd2df, an interactive commit 'fix(factory): cancel a running CI run for lacking a fix only once it has failed'. The 080214 coordinator was idle and polling, with no ci lines from 08:26 to 13:46. The next shift's coordinator read the run at 13:51:04 and logged 'cannot read its log with gh run view --log-failed' (134646.log:17; again at 145254.log:31), so no task was filed. The spell ended on run 41691198001 for ac11d86f, which itself took 133 minutes (18:10Z to 20:23Z). The failing test is unknown because the coordinator could not read the log.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-161553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-161553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41688300433; Select-String -Path Z:\repos\Curl.audit\20261103-161553\logs\DarkFactory-20261103-*.log -Pattern '41688300433'
```

- Expected: No red spell over 30 minutes; the failing test filed as a CI fix task
- Actual: Spell 122.97 min; coordinator lines say only 'cannot read its log with gh run view --log-failed'; no task filed

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the window's start: the fix's landing, or for a task with no fix yet the later of its fix's landing and ADR-1235's commit), gives the finding's expected result with CI red time counted only while a run on the branch has a failing test or build error (ADR-1235): a failed run whose only non-success jobs were never given a runner, ran into their `timeout-minutes` in a job that tests nothing, or were superseded by a newer commit is left out, as `gh run view <id> --json jobs` and each job's annotations classify it. Notes name the fix, its landing time, the window, each run left out and why, and the numbers. If no factory shift has run since the window's start, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
