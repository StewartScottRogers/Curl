---
id: BL-5191
title: Fix AF-0746: CI red 64.65 minutes from run 43305315824 (c7870da8) with no coordinator trace in the log window
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
# BL-5191 — Fix AF-0746: CI red 64.65 minutes from run 43305315824 (c7870da8) with no coordinator trace in the log window

## Goal

The cause of the factory behaviour the audit office reported as AF-0746 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0746 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0746-ci-red-64-65-minutes-from-run-43305315824-c7870da8.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

Run 43305315824 failed, ended 2026-11-12T21:14:44Z (14:14 local). The spell ran to 22:19:23Z, 64.65 minutes. No ci trace line names the run in any DarkFactory log, since the first log starts at 20:00 local, after the run. The next green run came more than 30 minutes after.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-12 -LogRoot <logs> -CiRunsJson <logs>/ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 43305315824
```

- Expected: No spell, or the failure filed within 30 minutes of the run ending.
- Actual: Spell of 64.65 minutes.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
