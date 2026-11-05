---
id: BL-4877
title: Fix AF-0686: Lanes waited 73 of 329.9 lane-minutes (22.1%) on overlaps from whole-project touches in shift 174735
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-05
completed:
---
# BL-4877 — Fix AF-0686: Lanes waited 73 of 329.9 lane-minutes (22.1%) on overlaps from whole-project touches in shift 174735

## Goal

The cause of the factory behaviour the audit office reported as AF-0686 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0686 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0686-lanes-waited-73-of-329-9-lane-minutes-22-1-on-over.md`.

Location: `logs/DarkFactory-20261104-174735-L7.log:18`

Location: `logs/DarkFactory-20261104-174735-L7.log:18`

waitOverlapMinutes 73, laneMinutes 329.92 (22.1%; 24.5% of 297.9 if the doubtful L9 log is left out). Every minute falls in shift 174735 from 17:56 to 18:27, while lanes 1, 2, 3, 4, 7 and 8 logged 'No task can start yet: every ready task overlaps one in Doing'. During that time lanes 5 and 6 worked through the ready tasks one at a time. Cause, from the task files' touches:
- BL-4857 and BL-4858 each touch all of [Curl.Conformance.LibTest.UnitLibrary], which overlaps BL-4856 [Curl.Conformance.LibTest.UnitLibrary, ...]. Their runs edited only Lib500.cs/Lib500Tests.cs (L6.log:20-25) and Lib1515.cs/Lib1512To1523Tests.cs (L6.log:38-43), against BL-4856's Lib1540.cs/Lib1535To1550Tests.cs. So the three ran one after another on lane 6 (18:16:24, 18:31:03).
- BL-4861 touches all of [Curl.Engine.UnitLibrary], which overlaps BL-4860. It edited only WriteGate.cs/WriteGateTests.cs, against BL-4860's ManagedMultiTransfer.Timer.cs, and was claimed only at 18:20:01 (L5.log:25).
File-level touches would have been true and disjoint, so idle lanes could have taken all three at once. The coordinator retired lanes because of this (174735.log:19-31, 'lanes 8 -> 6', 'lanes 6 -> 2'). CLAUDE.md says touches name files or narrow subfolders, never a whole project.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-04 -LogRoot Z:\repos\Curl.audit\20261105-081937\logs -CiRunsJson Z:\repos\Curl.audit\20261105-081937\logs\ci-runs.json -OutFile $env:TEMP\process.json; $j = Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json; '{0} of {1}' -f $j.waitOverlapMinutes, $j.laneMinutes; Select-String -Path Z:\repos\Curl.audit\20261105-081937\logs\DarkFactory-20261104-174735-L*.log -Pattern 'overlaps one in Doing'
```

- Expected: waitOverlapMinutes at most 20% of laneMinutes
- Actual: 73 of 329.92 (22.1%); 25 overlap-wait lines across lanes 1, 2, 3, 4, 7 and 8 from 17:56 to 18:27

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-05: Created.
