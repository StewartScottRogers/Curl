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
completed:
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

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
