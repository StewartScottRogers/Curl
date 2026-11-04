---
id: BL-4797
title: Fix AF-0624: CI red 171.72 minutes from run 41655977963 (85f7b9d8): Linux-only progress-meter test failure, then failure logs the coordinator could not read while an idle coordinator stopped watching CI
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
# BL-4797 — Fix AF-0624: CI red 171.72 minutes from run 41655977963 (85f7b9d8): Linux-only progress-meter test failure, then failure logs the coordinator could not read while an idle coordinator stopped watching CI

## Goal

The cause of the factory behaviour the audit office reported as AF-0624 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0624 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0624-ci-red-171-72-minutes-from-run-41655977963-85f7b9d.md`.

Location: `logs/DarkFactory-20261103-080214.log:17`

Location: `logs/DarkFactory-20261103-080214.log:17`

ciRedSpells: 2026-11-03T15:01:02Z to 17:52:45Z (08:01 to 10:52 Arizona), 171.72 min, runId 41655977963. The run failed on Linux in CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter at commit 85f7b9d8 (BL-4729, a gRPC fix). BL-4732 was filed at 08:01 (c6ca89a8f), claimed on lane 1 at 08:02 and fixed at 08:04 (faccb10d4). The next run, 41660275371 on fe004e97, failed at 08:25. The coordinator logged 'merge not merged: CI failure on fe004e9' and 'run 41660275371 failure ... cannot read its log with gh run view --log-failed' (080214.log:16-17), filed nothing, and at 08:26 entered 'nothing claimable; polling the board every 5 minutes'. It wrote no further ci lines until 13:46. Runs 41669804192 (429184b1) and 41671248257 (cfbd85f7) also failed with nobody watching. CI turned green only at 10:52 Arizona, on run 41682710305 for f4a738a9, an interactive master merge. No merge to master went through while CI was red.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-161553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-161553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41655977963; Select-String -Path Z:\repos\Curl.audit\20261103-161553\logs\DarkFactory-20261103-080214.log -Pattern ' ci | merge |polling'
```

- Expected: No red spell over 30 minutes; a CI fix task filed and landed for each failing run
- Actual: Spell 171.72 min; runs 41660275371, 41669804192, 41671248257 failed with no task filed; no ci lines between 08:26 and 13:46

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the window's start: the fix's landing, or for a task with no fix yet the later of its fix's landing and ADR-1235's commit), gives the finding's expected result with CI red time counted only while a run on the branch has a failing test or build error (ADR-1235): a failed run whose only non-success jobs were never given a runner, ran into their `timeout-minutes` in a job that tests nothing, or were superseded by a newer commit is left out, as `gh run view <id> --json jobs` and each job's annotations classify it. Notes name the fix, its landing time, the window, each run left out and why, and the numbers. If no factory shift has run since the window's start, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
