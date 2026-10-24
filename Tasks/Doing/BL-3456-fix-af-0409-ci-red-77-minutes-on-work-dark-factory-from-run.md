---
id: BL-3456
title: Fix AF-0409: CI red 77 minutes on work/dark-factory from run 40021720442 (Conversation_AnswersAsRecorded on macOS); 23 runs cancelled before the first green
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
# BL-3456 — Fix AF-0409: CI red 77 minutes on work/dark-factory from run 40021720442 (Conversation_AnswersAsRecorded on macOS); 23 runs cancelled before the first green

## Goal

The cause of the factory behaviour the audit office reported as AF-0409 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0409 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0409-ci-red-77-minutes-on-work-dark-factory-from-run-40.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-23T21:02:11Z to 22:19:09Z, 76.97 min, run 40021720442 on 2640c8ca, macOS only. The coordinator waited for a second platform's result, then filed BL-3415 at 14:09:25 local, 7 min after the red (DarkFactory-20261023-110315.log:82-86). L1 claimed it at 14:22:28 and it was Done at 14:29:59. Runs 40023582861 (989333e7) and 40026750438 (fff2f3b1), both created before the fix, failed as well. Between 20:45Z and 21:51Z, ci-runs.json shows 23 cancelled runs. The first run that finished after the fix, 40029783223, was created at 21:51:49Z, so the spell lasted about 50 min longer than the fix took.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 40021720442
```

- Expected: no ciRedSpells entry over 30 minutes
- Actual: start 2026-10-23T21:02:11Z end 22:19:09Z minutes 76.97 runId 40021720442

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. interactive session: CI watch fix for red spells Criteria at claim: 87cd20bd 9742157d.
