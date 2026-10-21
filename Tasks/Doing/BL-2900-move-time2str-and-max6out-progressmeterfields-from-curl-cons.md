---
id: BL-2900
title: Move time2str and max6out (ProgressMeterFields) from Curl.Console into Curl.Output for unit1636
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/ProgressMeterFields.cs, Curl.Console.UnitTests/ProgressMeterFieldsTests.cs, Curl.Output.UnitLibrary/ProgressMeterFields.cs, Curl.Output.UnitTests/ProgressMeterFieldsTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2900 — Move time2str and max6out (ProgressMeterFields) from Curl.Console into Curl.Output for unit1636

## Goal

curl's `time2str` and `max6out` (Curl's `ProgressMeterFields`, now in `Curl.Console`) live in `Curl.Output.UnitLibrary`, so the port of `tests/unit/unit1636.c` (BL-2845) can call them.

## Context

`Curl.Conformance.UnitProgram.UnitLibrary` cannot reference the executable `Curl.Console`. Move the type (or its two formatters) into `Curl.Output`, keep `Curl.Console` calling it, and check it against test1636.curltest's stdout block (63 doubling values for each, plus five byte counts). Upstream: https://github.com/curl/curl/blob/curl-8_22_0/lib/progress.c.

## Acceptance criteria

- [ ] `time2str` and `max6out` counterparts are in `Curl.Output.UnitLibrary` and give test1636's stdout values; `Curl.Console` uses them from there.
- [ ] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [ ] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
- 2026-10-21: Backlog -> Doing. Criteria at claim: 5fcee81b 3d228e1f d3a9ab15 099d1d27.
