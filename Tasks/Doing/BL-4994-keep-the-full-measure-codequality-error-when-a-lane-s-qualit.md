---
id: BL-4994
title: Keep the full Measure-CodeQuality error when a lane's quality measure writes no report, and fix its cause
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Integrate.ps1, Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-11
completed:
---
# BL-4994 — Keep the full Measure-CodeQuality error when a lane's quality measure writes no report, and fix its cause

## Goal

A lane's quality measure before merging either writes its report or leaves the full error where it can be read, and the cause of today's intermittent failures is fixed.

## Context

On 2026-11-11, 19 of 192 measurements before a merge (shifts 20261111-082724 and -101854) traced `quality no report (left to build and tests); it said: ...` with only 60 characters of the error, e.g. `At Z:\repos\Curl.lanes\lane-1\Measure-CodeQ~` or `+ ~~~~~~~~~~~~~~~~`: a PowerShell error from `Measure-CodeQuality.ps1`. `Test-QualityGates` in `DarkFactory/Integrate.ps1` (~line 158) cuts the output with `Get-Short ... 60` and then integrates anyway, so the gates are left to CI's quality job.

Run by hand on the main checkout (`Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Rtsp.UnitLibrary -JsonPath <tmp>`) the measure succeeded, so the failure is intermittent and lane-only. Likely causes to check: two lanes measuring at once and sharing a temp or coverage path, a locked file, or the 2026-11-11 morning without the .NET 10.0.4xx SDK (some events predate the 09:20 install).

## Acceptance criteria

- [x] When the measure writes no report, the lane saves its whole output to a file in the shift's log folder (e.g. `<repo>.logs\quality-<id>-<stamp>.txt`) and the trace names that file.
- [x] The cause of the failures is found from that output or a reproduction, named in Notes, and fixed, or filed as its own task if it lies outside these files.
- [x] `RunDarkFactory.ps1 -TestIntegrateLock` passes, with a case for a measure that writes no report.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: `Write-Trace` cuts every line at 118 characters and `Test-QualityGates` took the last 5 output lines. A `throw` in Measure-CodeQuality.ps1 (e.g. "dotnet build/test failed with exit code N") prints its message first and the `At ...:line char` and `+ ~~~~` pointer lines after, so the traced 60 characters were always the pointer, never the message. The failures are the script's own build or test failures (a lane building while others build, or the SDK missing before 09:20); the full message is now kept, and no change to Measure-CodeQuality.ps1 is needed.
- Fix: on no report the whole output is saved to `<repo>.logsquality-<id>-<stamp>.txt`; the trace names the file (leaf name first, so the cap keeps it) and shows the last non-pointer line. `-TestIntegrateLock` has a case for it and passes. Only PowerShell changed, so no C# build or test was affected.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 446f37f3 cdbc8f7d 17ab7173 a0b49a29.
