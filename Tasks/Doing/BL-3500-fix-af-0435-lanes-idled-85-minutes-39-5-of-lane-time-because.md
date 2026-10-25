---
id: BL-3500
title: Fix AF-0435: Lanes idled 85 minutes (39.5% of lane time) because every ready Backlog task was interactive only
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Claim.ps1, DarkFactory/Park.ps1, Documentation/Planning/Decisions/ADR-0813-a-lane-ends-when-only-interactive-work-is-left.md]
lane: no
requirement: none
created: 2026-10-25
completed:
---
# BL-3500 — Fix AF-0435: Lanes idled 85 minutes (39.5% of lane time) because every ready Backlog task was interactive only

## Goal

The cause of the factory behaviour the audit office reported as AF-0435 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0435 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0435-lanes-idled-85-minutes-39-5-of-lane-time-because-e.md`.

Location: `logs/DarkFactory-20261024-185147-L1.log`

Location: `logs/DarkFactory-20261024-185147-L1.log`

waitNothingReadyMinutes=85 of laneMinutes=215.25 (39.5%, over 20%). Lanes 1, 2 and 3 logged 'No task is ready. Backlog is interactive only, waiting on Stewart or an interactive session' about once a minute: L1 from 19:13:32 to 20:17:29, then 'shift end (nothing ready)' at 20:18:35; L2 from 19:11:24 to 19:24:17; L3 from 19:10:54 to 19:23:33. The board at commit 975151e (19:13:02) held 13 Backlog tasks: BL-3363, BL-3364, BL-3365, BL-3451, BL-3452, BL-3453, BL-3454, BL-3455, BL-3456 and BL-3457 (each touches [RunDarkFactory.ps1], lane: no), BL-3464 (touches .github/workflows/ci.yml, a guard file, lane: no), BL-3468 (lane: no, requeued because the audit guard refuses Invoke-MutationTest.ps1) and BL-3472 (touches .claude, a guard path). BL-3465 was in Doing with lane: no. The coordinator logged 'Audit due: factory-script, reaudit-pending:8, interactive-stuck:1' at 20:19:03. The next shift then started twice (DarkFactory-20261024-203407.log 20:34:11 and DarkFactory-20261024-203504.log 20:35:06) and was refused both times: 'BL-3472 is in Doing and held by no lane'. Each refusal ran all four alarm stages, so no lane work followed. With lane 9's unsupported 47.6 minutes left out, the share is still about 51%.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-24 -LogRoot <log folder> -CiRunsJson <log folder>\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path <log folder>\DarkFactory-20261024-185147-L*.log -Pattern 'Backlog is interactive only' | Measure-Object
```

- Expected: waitNothingReadyMinutes 20% of laneMinutes or less.
- Actual: waitNothingReadyMinutes 85 of laneMinutes 215.25 (39.5%); the 13 Backlog tasks were all lane: no or touched guard paths.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
