---
id: BL-2899
title: Update the parallel progress meter time field to curl 8.22.0 timebuf for tool1622
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/ParallelProgressMeterText.cs, Curl.Output.UnitTests/ParallelProgressMeterTextTests.cs, Curl.Output.UnitTests/ParallelProgressMeterTextAdversarialTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2899 — Update the parallel progress meter time field to curl 8.22.0 timebuf for tool1622

## Goal

`ParallelProgressMeterText.Time` formats seconds as curl 8.22.0's `timebuf` in `src/tool_progress.c` does, so the port of `tests/tunit/tool1622.c` (BL-2845) passes.

## Context

Curl's `Time` follows 8.21.0 (`ddd hhh`, then days cut to eight). 8.22.0's tool1622 expects months and years: 134217727 is ` 51m 23d`, 4294967295 is `    136y`, 4398046511103 is ` >99999y`; the expected stdout for 63 doubling values is in `Curl.Conformance.UnitTests/UpstreamTestData/test1622.curltest`. `Size` (max5data) already matches the C table. Source: https://github.com/curl/curl/blob/curl-8_22_0/src/tool_progress.c. Where a -Z progress test pins the 8.21 text, measure real curl and record the change in an ADR.

## Acceptance criteria

- [ ] `ParallelProgressMeterText.Time` gives every value of test1622's `timebuf` stdout block and of tool1622.c's `timecases` table.
- [ ] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [ ] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
