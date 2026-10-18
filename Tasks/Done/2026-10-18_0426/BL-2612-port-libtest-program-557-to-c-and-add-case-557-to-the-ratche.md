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
completed: 2026-10-18
---
# BL-2612 — Port libtest program 557 to C# and add case 557 to the ratchet

## Goal

`lib557.c` (curl 8.22.0 `tests/libtest`, the `curl_m*printf` checks) is a C# `ILibTest` and case 557 is on the ratchet.

## Context

Split out of BL-2531 (batch 5 of the libtest ports, phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md`, ADR-0506): lib557 tests libcurl's own printf, which Libcurl gains in BL-2616. Port it as `Lib557` in `Curl.Conformance.LibTest.UnitLibrary`, register it in `LibTestPrograms.Ported`, and test it in `Curl.Conformance.LibTest.UnitTests` like the other ports. Case 557's expected stdout lists the "All ... tests OK!" lines. lib557 is 1422 lines of tables; keep each method at complexity 10 or less by driving the tables through one checker.

## Acceptance criteria

- [x] `Lib557` is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in `LibTestPrograms.Ported`, with focused unit tests in `Curl.Conformance.LibTest.UnitTests`.
- [x] Case 557 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or, failing, is filed as a task with its differing lines.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Upstream lib557.c is not vendored; the port carries its checks as tables (Lib557Group of Lib557Check) through one CheckGroup, taking the string, float, weird, positional, return-code and v-function rows Libcurl.UnitTests already pins from lib557. The integer tables are built per width from upstream's bit patterns (max, each nibble at each position, 0, and the negated less-one values), each expected text the invariant decimal.
- Choice: long is 32 bits on Windows and 64 elsewhere (SIZEOF_LONG picks the table, as Libcurl's PrintfPlatform.Current does); curl_off_t is formatted with %lld, 64 bits on every platform, rather than the per-platform CURL_FORMAT_CURL_OFF_T (I64d / ld), so one format serves all three CI platforms.
- Case 557 passes through the runner on Windows; stdout is platform-neutral (only the OK lines), so it is on the ratchet.
- Measure-CodeQuality: 100% line, 100% branch, 0 failing members (worst CRAP 10).

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: a5fdd076 e20ddf2b e39e1cd8 4272a6b9.
- 2026-10-18: Doing -> Done. lib557 runs as the C# Lib557 over Libcurl's printf and case 557 passes on the ratchet
