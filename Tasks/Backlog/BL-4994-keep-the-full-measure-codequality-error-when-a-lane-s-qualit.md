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

- [ ] When the measure writes no report, the lane saves its whole output to a file in the shift's log folder (e.g. `<repo>.logs\quality-<id>-<stamp>.txt`) and the trace names that file.
- [ ] The cause of the failures is found from that output or a reproduction, named in Notes, and fixed, or filed as its own task if it lies outside these files.
- [ ] `RunDarkFactory.ps1 -TestIntegrateLock` passes, with a case for a measure that writes no report.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-11: Created.
