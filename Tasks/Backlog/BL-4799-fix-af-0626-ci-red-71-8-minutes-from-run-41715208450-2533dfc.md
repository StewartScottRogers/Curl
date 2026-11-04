---
id: BL-4799
title: Fix AF-0626: CI red 71.8 minutes from run 41715208450 (2533dfc6): two failures whose logs the coordinator could not read, no CI task filed
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
# BL-4799 — Fix AF-0626: CI red 71.8 minutes from run 41715208450 (2533dfc6): two failures whose logs the coordinator could not read, no CI task filed

## Goal

The cause of the factory behaviour the audit office reported as AF-0626 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0626 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0626-ci-red-71-8-minutes-from-run-41715208450-2533dfc6.md`.

Location: `logs/DarkFactory-20261103-145254.log:30`

Location: `logs/DarkFactory-20261103-145254.log:30`

ciRedSpells: 2026-11-03T21:35:59Z to 22:47:47Z (14:35 to 15:47 Arizona), 71.8 min, runId 41715208450 on 2533dfc6 ('chore(tasks): file BL-4734 to BL-4778 ...'). The coordinator logged 'cannot read its log with gh run view --log-failed' for it at 14:58:53 (145254.log:30) and for run 41723085402 on 441bcff2 at 15:22:12 (145254.log:38), and filed no CI task for either. The only CI-related task in the window, BL-4780 (flaky MissedPing Socket.IO test), was filed at 15:09 from BL-4755's local run, not by the CI watch. Green returned at 15:47 on run 41729449902 (5d1b4dcf). Run 41735630431 (fad49f8f) failed again at 16:13 on IdleTimeout_ContainerAsks1000msAndWaits2500..., so CI was red again at the end of the window.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-161553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-161553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41715208450; Select-String -Path Z:\repos\Curl.audit\20261103-161553\logs\DarkFactory-20261103-145254.log -Pattern ' ci '
```

- Expected: No red spell over 30 minutes; each failing run's test filed as a CI fix task
- Actual: Spell 71.8 min; runs 41715208450 and 41723085402 logged as 'cannot read its log'; no task filed

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
