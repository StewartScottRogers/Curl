---
id: BL-3455
title: Fix AF-0408: CI red 41 minutes on work/dark-factory from run 40008612979 (three VerifyPeer_CaCertificateFileHoldingOnly* partial-chain tests)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-24
completed:
---
# BL-3455 — Fix AF-0408: CI red 41 minutes on work/dark-factory from run 40008612979 (three VerifyPeer_CaCertificateFileHoldingOnly* partial-chain tests)

## Goal

The cause of the factory behaviour the audit office reported as AF-0408 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0408 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0408-ci-red-41-minutes-on-work-dark-factory-from-run-40.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-23T19:28:00Z to 20:08:59Z, 40.98 min, run 40008612979 on a07ed80b. The coordinator filed BL-3346, BL-3347 and BL-3348 at 12:34:23 local, 6 min after the red (DarkFactory-20261023-110315.log:52-55). L1 fixed them between 12:35:51 and 12:42:58 (BL-3347 closed as 'Already fixed by BL-3346'). A second failing run, 40011412660 on 9e379e12, was pushed before the fix. Green at 13:08 local.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 40008612979
```

- Expected: no ciRedSpells entry over 30 minutes
- Actual: start 2026-10-23T19:28:00Z end 20:08:59Z minutes 40.98 runId 40008612979

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. interactive session: CI watch fix for red spells Criteria at claim: 87cd20bd 9742157d.
