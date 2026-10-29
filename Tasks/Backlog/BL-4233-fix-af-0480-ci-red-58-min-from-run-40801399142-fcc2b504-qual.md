---
id: BL-4233
title: Fix AF-0480: CI red 58 min from run 40801399142 (fcc2b504, Quality gates windows-latest: Curl.Core and Curl.Engine outside the gate)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-29
completed:
---
# BL-4233 — Fix AF-0480: CI red 58 min from run 40801399142 (fcc2b504, Quality gates windows-latest: Curl.Core and Curl.Engine outside the gate)

## Goal

The cause of the factory behaviour the audit office reported as AF-0480 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0480 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0480-ci-red-58-min-from-run-40801399142-fcc2b504-qualit.md`.

Location: `logs/DarkFactory-20261028-172341.log:65`

Location: `logs/DarkFactory-20261028-172341.log:65`

Spell 2026-10-29T01:56:20Z to 02:54:15Z (18:56-19:54 Arizona), 57.92 min, on fcc2b504 (BL-4006's test change). BL-4017 was filed at 19:03:57 (DarkFactory-20261028-172341.log:65). It added two tests for Curl.Core and Curl.Engine coverage. Lanes do not run Measure-CodeQuality for every library their change affects, so coverage gaps first show in CI.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40801399142
```

- Expected: No red spell over 30 minutes.
- Actual: minutes 57.92, runId 40801399142.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
