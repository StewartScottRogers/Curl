---
id: BL-5002
title: Fix AF-0705: CI red 94.5 minutes from run 42239087175 (e9ba9daf, SocketAction_TimeoutAgainstASilentServer_TellsTheTimeout)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-12
completed:
---
# BL-5002 — Fix AF-0705: CI red 94.5 minutes from run 42239087175 (e9ba9daf, SocketAction_TimeoutAgainstASilentServer_TellsTheTimeout)

## Goal

The cause of the factory behaviour the audit office reported as AF-0705 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0705 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0705-ci-red-94-5-minutes-from-run-42239087175-e9ba9daf.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-11-06T15:19:48Z to 16:54:19Z, 94.52 minutes, run 42239087175 on e9ba9daf. The lane logs name test SocketAction_TimeoutAgainstASilentServer_TellsTheTimeout.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-05 -LogRoot <logs> -CiRunsJson <logs>/ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object minutes -gt 30
```

- Expected: No spell over 30 minutes
- Actual: A 94.52-minute spell at run 42239087175

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-12: Created.
