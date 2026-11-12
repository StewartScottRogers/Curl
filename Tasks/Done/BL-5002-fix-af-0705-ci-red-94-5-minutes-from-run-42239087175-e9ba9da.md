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
completed: 2026-11-12
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

- [x] Every CI failure on `work/dark-factory` in a window of the live logs written after the fix landed is filed as a task, covered by a live task, or green again within 30 minutes of its run ending (Stewart, 2026-11-12: "every failure filed within 30 minutes", replacing "no red spell over 30 minutes", which CI's own ~35-minute run makes unreachable); Notes name the window and the slowest time.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Measured 2026-11-12 against the new criterion over 2026-11-11 15:00Z onward (shifts 20261111-101854 and -190859; `gh run list --workflow CI --branch work/dark-factory` and the coordinators' "ci" trace lines): 14 failed CI runs, every one read by the coordinator within 0-22 minutes of ending. The slowest was run 43060203629 (Quality gates; ended 10:05 Arizona, filed as BL-4969 at 10:27, 22 minutes). The rest were filed (BL-4974, BL-4995, BL-4997/BL-4998), covered by a live task, or green again within minutes. The criterion holds.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 9ae5ba4d 9742157d.
- 2026-11-12: Doing -> Done. Every CI failure since 2026-11-11 15:00Z was filed, covered or green within 30 minutes (slowest 22 min, run 43060203629 -> BL-4969); build clean, 115 fast test projects pass at c5b05b80b. The finding closes only on the process auditor's re-audit.
