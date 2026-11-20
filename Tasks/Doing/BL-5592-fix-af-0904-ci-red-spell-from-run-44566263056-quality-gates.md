---
id: BL-5592
title: Fix AF-0904: CI red spell from run 44566263056 (Quality gates, Windows) was first traced 3h27m after the run ended; no coordinator ran in between
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-19
completed:
---
# BL-5592 — Fix AF-0904: CI red spell from run 44566263056 (Quality gates, Windows) was first traced 3h27m after the run ended; no coordinator ran in between

## Goal

The cause of the factory behaviour the audit office reported as AF-0904 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0904 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0904-ci-red-spell-from-run-44566263056-quality-gates-wi.md`.

Location: `logs/DarkFactory-20261119-134021.log:13`

Location: `logs/DarkFactory-20261119-134021.log:13`

ciRedSpells[0]: start 2026-11-19T17:19:15Z, end 21:11:44Z, 232.48 min, runId 44566263056. Run 44566263056 ended 17:19:15Z (10:19 local); runs 44585647013 (ended 18:42Z) and 44586512674 (ended 19:10Z) also failed inside the spell. The first coordinator ci trace naming any of them is 13:46:12 (DarkFactory-20261119-134021.log), 3h27m, 2h04m and 1h36m after their ends. The previous coordinator log (DarkFactory-20261119-082213.log) ends at 10:19:25 on handover, and the next coordinator started 13:40:24, so nobody watched CI for about 3h20m. The 30-minute rule is crossed. The 14:12:12 trace is the pass-since line. The runner-capacity run 44557476016 is infrastructure and excluded (ADR-1235).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-19 -LogRoot <log folder> -CiRunsJson <log folder>\ci-runs.json -OutFile process.json; Select-String -Path <log folder>\DarkFactory-20261119-*.log -Pattern '44566263056'
```

- Expected: A ci trace line naming the run (filed, covered or passed since) within 30 minutes of the run ending.
- Actual: First trace 13:46:12 local, run ended 10:19 local; ciRedSpells[0] is 232.48 minutes.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
