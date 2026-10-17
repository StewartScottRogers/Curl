---
id: BL-2612
title: Port libtest program 557 to C# and add case 557 to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2616]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2612 — Port libtest program 557 to C# and add case 557 to the ratchet

## Goal

`lib557.c` (curl 8.22.0 `tests/libtest`, the `curl_m*printf` checks) is a C# `ILibTest` and case 557 is on the ratchet.

## Context

Split out of BL-2531 (batch 5 of the libtest ports, phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md`, ADR-0506): lib557 tests libcurl's own printf, which Libcurl gains in BL-2616. Port it as `Lib557` in `Curl.Conformance.LibTest.UnitLibrary`, register it in `LibTestPrograms.Ported`, and test it in `Curl.Conformance.LibTest.UnitTests` like the other ports. Case 557's expected stdout lists the "All ... tests OK!" lines. lib557 is 1422 lines of tables; keep each method at complexity 10 or less by driving the tables through one checker.

## Acceptance criteria

- [ ] `Lib557` is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in `LibTestPrograms.Ported`, with focused unit tests in `Curl.Conformance.LibTest.UnitTests`.
- [ ] Case 557 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or, failing, is filed as a task with its differing lines.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
