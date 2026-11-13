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
completed:
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

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-13: Created.
