---
id: BL-5175
title: Re-fix AF-0706: CI red 131.8 minutes from run 43060203629 (d2e24184, Quality gates Measure coverage)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-14
completed:
---
# BL-5175 — Re-fix AF-0706: CI red 131.8 minutes from run 43060203629 (d2e24184, Quality gates Measure coverage)

## Goal

The cause of the factory behaviour the audit office reported as AF-0706 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0706 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0706-ci-red-131-8-minutes-from-run-43060203629-d2e24184.md`.

Re-fix: the earlier task(s) BL-5003 reached Done, and a later re-audit by the process auditor found the reproduction still reproduces:

- 2026-11-13 | 2026-11-13_2321.md | reproduces: yes | Ran the reproduction with -Since 2026-11-05: run 43060203629 is still in ciRedSpells at 131.75 minutes. The window also holds new CI-red spells that crossed the rule (runs 43305315824 and 43320342510, no trace).

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-11-11T17:05:30Z to 19:17:15Z, 131.75 minutes, run 43060203629 on d2e24184. The 20261111-082724 and 101854 logs show job 'Quality gates (windows-latest) step Measure coverage' at 10:27, 10:47, 11:13 and 11:22. BL-4969 was filed at 10:27:43.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-05 -LogRoot <logs> -CiRunsJson <logs>/ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object minutes -gt 30
```

- Expected: No spell over 30 minutes
- Actual: A 131.75-minute spell at run 43060203629

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
