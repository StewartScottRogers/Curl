---
id: AF-0686
title: Lanes waited 73 of 329.9 lane-minutes (22.1%) on overlaps from whole-project touches in shift 174735
auditor: process
severity: Medium
status: closed
reason: Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces.
key: process:logs:whole-project-touches:overlap-wait
reproduction: none
task: BL-4877
tasks: BL-4877
found: 2026-11-05
found-at: aea9f0110c59ad5209aafb9f0f985886a2cb9439
scorecard: 2026-11-05_0819.md
duplicate-of:
closed: 2026-11-05
closed-how: reliable-reaudit
closed-by: 2026-11-05_2025.md
---
# AF-0686 - Lanes waited 73 of 329.9 lane-minutes (22.1%) on overlaps from whole-project touches in shift 174735

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261104-174735-L7.log:18`: Lanes waited 73 of 329.9 lane-minutes (22.1%) on overlaps from whole-project touches in shift 174735.

## Evidence

Location: `logs/DarkFactory-20261104-174735-L7.log:18`

waitOverlapMinutes 73, laneMinutes 329.92 (22.1%; 24.5% of 297.9 if the doubtful L9 log is left out). Every minute falls in shift 174735 from 17:56 to 18:27, while lanes 1, 2, 3, 4, 7 and 8 logged 'No task can start yet: every ready task overlaps one in Doing'. During that time lanes 5 and 6 worked through the ready tasks one at a time. Cause, from the task files' touches:
- BL-4857 and BL-4858 each touch all of [Curl.Conformance.LibTest.UnitLibrary], which overlaps BL-4856 [Curl.Conformance.LibTest.UnitLibrary, ...]. Their runs edited only Lib500.cs/Lib500Tests.cs (L6.log:20-25) and Lib1515.cs/Lib1512To1523Tests.cs (L6.log:38-43), against BL-4856's Lib1540.cs/Lib1535To1550Tests.cs. So the three ran one after another on lane 6 (18:16:24, 18:31:03).
- BL-4861 touches all of [Curl.Engine.UnitLibrary], which overlaps BL-4860. It edited only WriteGate.cs/WriteGateTests.cs, against BL-4860's ManagedMultiTransfer.Timer.cs, and was claimed only at 18:20:01 (L5.log:25).
File-level touches would have been true and disjoint, so idle lanes could have taken all three at once. The coordinator retired lanes because of this (174735.log:19-31, 'lanes 8 -> 6', 'lanes 6 -> 2'). CLAUDE.md says touches name files or narrow subfolders, never a whole project.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-04 -LogRoot Z:\repos\Curl.audit\20261105-081937\logs -CiRunsJson Z:\repos\Curl.audit\20261105-081937\logs\ci-runs.json -OutFile $env:TEMP\process.json; $j = Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json; '{0} of {1}' -f $j.waitOverlapMinutes, $j.laneMinutes; Select-String -Path Z:\repos\Curl.audit\20261105-081937\logs\DarkFactory-20261104-174735-L*.log -Pattern 'overlaps one in Doing'
```

- Expected: waitOverlapMinutes at most 20% of laneMinutes
- Actual: 73 of 329.92 (22.1%); 25 overlap-wait lines across lanes 1, 2, 3, 4, 7 and 8 from 17:56 to 18:27

## Re-audits

- 2026-11-05 | 2026-11-05_2025.md | reproduces: no | The given reproduction over the old window still gives '73 of 329.92' and 24 'overlaps one in Doing' lines (historical). By mechanism over this window, which holds shifts 150256, 172904 and 193240 finishing 11 tasks: waitOverlapMinutes 35 of laneMinutes 597 (5.9%), under the 20% bar. The 20 overlap-wait lines are short waits at 15:34 to 15:38 and 17:42, not whole-project touches serialising lanes.

## Log

- 2026-11-05: filed proposed.
- 2026-11-05: accepted -> closed. Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces.
