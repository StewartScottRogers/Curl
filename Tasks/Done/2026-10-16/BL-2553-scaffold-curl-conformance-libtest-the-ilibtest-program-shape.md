---
id: BL-2553
title: Scaffold Curl.Conformance.LibTest: the ILibTest program shape and result codes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx, Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2553 — Scaffold Curl.Conformance.LibTest: the ILibTest program shape and result codes

## Goal

A new `Curl.Conformance.LibTest.UnitLibrary` (and `.UnitTests`) exists with the `ILibTest` interface a ported libtest program implements, a `LibTestContext` carrying the program's arguments, the test number and the case's URL, and `LibTestResult` holding the process exit code a `main()` would return.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Use the 
ew-project skill; the pair sorts between Curl.Conformance.Http3Server.UnitTests and Curl.Conformance.UnitLibrary in Curl.slnx. It references Libcurl and Curl.Protocol.Abstractions.UnitLibrary only.

## Acceptance criteria

- [x] `ILibTest`, `LibTestContext` and `LibTestResult` exist with XML docs and tests; the projects are in `Curl.slnx` in sorted order.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- Shape (defaults taken, small enough for no ADR): `ILibTest` has `ProgramName` (`lib500`, how the
  case runner of BL-2556 finds a port) and `RunAsync(LibTestContext, CancellationToken)` returning
  `Task<LibTestResult>`. `LibTestContext(testNumber, arguments)` copies the arguments after the program
  name, refuses a negative number, an empty list (no URL) or a null argument, and exposes `Url`
  (`argv[1]`) and `Argument2`-`Argument4` (`libtest_arg2`-`_arg4`, null when absent). `LibTestResult`
  is a record struct of the `main()` exit code with `Success`, `IsSuccess` and `FromCurlCode`
  (`Curl.Api.Codes.CurlCode`, reached through Libcurl); the `TEST_ERR_*` codes are BL-2554's.
- Measured: Curl.Conformance.LibTest.UnitLibrary 100% line, 100% branch, max complexity 9 summed
  over the library's members, 0 failing members. LibTest tests: 15 passed; every fast test project green;
  `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: b1c48a8f 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-16: Doing -> Done. Curl.Conformance.LibTest pair exists with ILibTest, LibTestContext and LibTestResult at 100% coverage
