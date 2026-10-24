---
id: BL-3457
title: Fix AF-0410: CI red 50 minutes on work/dark-factory from run 40034290028 (test1173 on Linux and macOS)
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
# BL-3457 — Fix AF-0410: CI red 50 minutes on work/dark-factory from run 40034290028 (test1173 on Linux and macOS)

## Goal

The cause of the factory behaviour the audit office reported as AF-0410 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0410 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0410-ci-red-50-minutes-on-work-dark-factory-from-run-40.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-23T22:57:04Z to 23:47:24Z, 50.33 min, run 40034290028 on 93ed8666. The coordinator filed BL-3420 at 16:00:26 local, 3 min after the red (DarkFactory-20261023-110315.log:118). L3 claimed it at 16:04:05 and it was Done at 16:13:29 ('test1173's man page scan runs in linear time'). Run 40035852439 on 710327b2, pushed before the fix, also failed. The first green run, 40038429558, was created at 23:13:30Z and finished at 23:47Z.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 40034290028
```

- Expected: no ciRedSpells entry over 30 minutes
- Actual: start 2026-10-23T22:57:04Z end 23:47:24Z minutes 50.33 runId 40034290028

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. interactive session: CI watch fix for red spells Criteria at claim: 87cd20bd 9742157d.
