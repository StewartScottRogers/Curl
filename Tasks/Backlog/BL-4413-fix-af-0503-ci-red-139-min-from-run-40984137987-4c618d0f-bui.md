---
id: BL-4413
title: Fix AF-0503: CI red 139 min from run 40984137987 (4c618d0f, Build_RelativeRealm_ReturnsNull on Linux and macOS, then the quality gates)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-31
completed:
---
# BL-4413 — Fix AF-0503: CI red 139 min from run 40984137987 (4c618d0f, Build_RelativeRealm_ReturnsNull on Linux and macOS, then the quality gates)

## Goal

The cause of the factory behaviour the audit office reported as AF-0503 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0503 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0503-ci-red-139-min-from-run-40984137987-4c618d0f-build.md`.

Location: `logs/DarkFactory-20261029-184521.log`

Location: `logs/DarkFactory-20261029-184521.log`

ciRedSpells: 2026-10-30T01:30:16Z to 03:49:07Z, 138.85 min, runId 40984137987, headSha 4c618d0f (18:30-20:49 MST 10-29). At 18:50:48 BL-4299 was filed (regression Build_RelativeRealm_ReturnsNull on Linux+macOS). L8 did not claim it until 19:31:03, 40 min later, and it was Done at 19:48:21. A second failure, the Quality gates (windows-latest) job, was filed as BL-4308 at 19:58:08 and Done at 20:33:48. Green at 20:49 MST. Lanes test only on Windows, so a Linux/macOS test and a coverage gate both reached the shared branch.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40984137987
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-30T01:30:16Z end 2026-10-30T03:49:07Z minutes 138.85 runId 40984137987

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
