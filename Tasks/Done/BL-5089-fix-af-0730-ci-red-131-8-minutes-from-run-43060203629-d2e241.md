---
id: BL-5089
title: Fix AF-0730: CI red 131.8 minutes from run 43060203629 (d2e24184); no coordinator trace filing it in the log window, next green 131.75 min after
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5089 — Fix AF-0730: CI red 131.8 minutes from run 43060203629 (d2e24184); no coordinator trace filing it in the log window, next green 131.75 min after

## Goal

The cause of the factory behaviour the audit office reported as AF-0730 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0730 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0730-ci-red-131-8-minutes-from-run-43060203629-d2e24184.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-11-11T17:05:30Z end 19:17:15Z, 131.75 min, runId 43060203629. Several failed runs (43064691654, 43070110531, 43074475535, 43077908998) lie inside the spell; the copied coordinator log starts 19:08 local on 11-11, so no filing line exists. Earliest bound is the next green run's end, 131.75 min after.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-11 -LogRoot <logs> -CiRunsJson <logs>/ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object minutes -gt 30
```

- Expected: No failed run stays unfiled for more than 30 minutes.
- Actual: Spell runId 43060203629, 131.75 minutes

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Same run as AF-0706, whose task closed 2026-11-12 under the same criterion (443ddc2f1); this finding re-reports the frozen 2026-11-11 window. Evidence is shared with BL-5090 and BL-5091.

- Fix: 3e4d0d43d, landed on work/dark-factory 2026-11-04 05:24:14 MST (the idle poll runs the CI watch), after BL-4783 (6f289fef1, 2026-11-03 16:32:49 MST: empty failure logs are read, not skipped).
- Window: 2026-11-12 14:00 to 2026-11-13 06:37 Arizona (shifts 20261112-130616, -184645, -200028, -211137, 20261113-045438), measured 2026-11-13 by an interactive session: `Measure-FactoryProcess.ps1 -Since 2026-11-12 -LogRoot Z:\repos\Curl.logs` with a fresh `gh run list --workflow CI --branch work/dark-factory ... --limit 500`, and each failed run matched to its first coordinator `ci` trace line.
- Numbers: 15 failed CI runs; every one was read by the coordinator between 7 minutes before its run's last update (a running run's failed jobs, ADR-0811) and 2 minutes after it. Each was filed (BL-5051, BL-5052, BL-5054, BL-5057, BL-5058, BL-5060), covered by one of those live tasks, or reported with no failing test or build error. Slowest from run end to filed task: 6 minutes (run 43323719038 ended 16:04, BL-5057 filed 16:10; run 43328655141 ended 16:41, BL-5058 filed 16:47). The measurement's raw red spells (64.65 min from 43305315824, 161.88 min from 43320342510) are fix-and-rerun time, which the 30-minute filing criterion (Stewart, 2026-11-12) does not count. Expected result holds.
- Build and fast tests at f605251df: `dotnet build` 0 errors; `dotnet test --filter "TestCategory!=Integration"` exit 0, no project failed.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-13: Doing -> Done. Every CI failure from 2026-11-12 14:00 to 2026-11-13 06:37 Arizona was filed or covered within 6 minutes of its run ending (15 runs); build and fast tests pass at f605251df. The finding closes only on the process auditor's re-audit.
