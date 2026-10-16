---
id: BL-2556
title: Curl.Conformance: run a lib tool case through its ported ILibTest program
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2555]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2556 — Curl.Conformance: run a lib tool case through its ported ILibTest program

## Goal

The upstream case runner, given a case whose `<client><tool>` is `lib%TESTNUMBER` or `libNNN`, looks up the ported program by number and runs it in-process over `Libcurl` against the case's servers, comparing stdout, protocol and `<stripfile>` parts as for a `curl` case; a lib case with no port stays skipped with the reason "libtest program not ported".

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Read Curl.Conformance.UnitTests/CLAUDE.md, UpstreamConformanceTests, UpstreamTestCase.cs and how the runner treats the <tool> section today. The runner keeps lib cases off PassingUpstreamCases.txt until a port lands, so HarnessReferencesTests and the ratchet must keep passing. A registry maps test number to ILibTest (no reflection scanning: AOT rules, an explicit table the port tasks append to).

## Acceptance criteria

- [x] A test registers a sample `ILibTest` and the runner runs a sample lib case through it; an unported lib case is skipped with the reason above.
- [x] `Curl.Conformance.UnitLibrary` and `Curl.Conformance.LibTest.UnitLibrary`: `Measure-CodeQuality.ps1 -Library` for each reports 0 failing members.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- Plan and decisions in ADR-0656. `LibTestPrograms` (LibTest library) is the explicit table keyed by
  `ProgramName`, `LibTestPrograms.Ported` the one port tasks append to (empty now). Keyed by name, not
  number, because `libNNN` programs serve several cases and the `<tool>` line names the program.
- `UpstreamCaseRunner` takes an optional `UpstreamLibTests(Programs, CreateEngine)`; a ported lib case
  runs on the same invocation (connectors, env, stdout) with the command's words as arguments and is
  judged as a curl case. `LibTestContext` gained `Engine`, `StandardOutput` and `StandardError` (init).
  The program's `curl_global_cleanup` disposes the engine, so the runner does not.
- Default taken: stderr writer uses `\n` line ends so `<stderr>` compares the same on every platform.
- Non-lib tools (`unit1300`-style, `tool%TESTNUMBER`, 2 cases) keep the old reason.
- Making a real Libcurl engine over the in-memory servers for the ratchet is filed as BL-2558 and added
  to BL-2527's `depends-on` (the port chain starts there). Until then the ratchet gives the runner no
  `UpstreamLibTests` and every lib case skips as "libtest program not ported".
- Measured: Measure-CodeQuality for both libraries, 100% line and branch, 0 failing members.
  Fast tests: 0 failures (Curl.Conformance.UnitTests 3143 passed, 641 skipped; LibTest 116 passed).
  `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 041385d4 236db91c 4272a6b9 8738c7f6.
- 2026-10-16: Doing -> Done. A <tool> lib case runs its ported ILibTest from LibTestPrograms over the case's invocation and is judged as a curl case; unported ones skip as 'libtest program not ported'
