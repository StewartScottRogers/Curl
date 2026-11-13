---
id: BL-5090
title: Fix AF-0731: CI red 74 minutes from run 43110153266 (27be8cbc); no filing trace, next green 74 min after
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
# BL-5090 — Fix AF-0731: CI red 74 minutes from run 43110153266 (27be8cbc); no filing trace, next green 74 min after

## Goal

The cause of the factory behaviour the audit office reported as AF-0731 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0731 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0731-ci-red-74-minutes-from-run-43110153266-27be8cbc-no.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-11-11T22:34:57Z to 23:48:57Z, 74 min, runId 43110153266. Failed runs 43112494560, 43114715030, 43115791546, 43117708980 inside the spell; no coordinator ci line in the copied logs names any of them; green run 43120906259 ended 23:48:57Z.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-11 -LogRoot <logs> -CiRunsJson <logs>/ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object minutes -gt 30
```

- Expected: Every failed run filed or green within 30 minutes of its end.
- Actual: Spell runId 43110153266, 74 minutes

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-13: Created.
