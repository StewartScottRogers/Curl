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
completed:
---
# BL-2556 — Curl.Conformance: run a lib tool case through its ported ILibTest program

## Goal

The upstream case runner, given a case whose `<client><tool>` is `lib%TESTNUMBER` or `libNNN`, looks up the ported program by number and runs it in-process over `Libcurl` against the case's servers, comparing stdout, protocol and `<stripfile>` parts as for a `curl` case; a lib case with no port stays skipped with the reason "libtest program not ported".

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Read Curl.Conformance.UnitTests/CLAUDE.md, UpstreamConformanceTests, UpstreamTestCase.cs and how the runner treats the <tool> section today. The runner keeps lib cases off PassingUpstreamCases.txt until a port lands, so HarnessReferencesTests and the ratchet must keep passing. A registry maps test number to ILibTest (no reflection scanning: AOT rules, an explicit table the port tasks append to).

## Acceptance criteria

- [ ] A test registers a sample `ILibTest` and the runner runs a sample lib case through it; an unported lib case is skipped with the reason above.
- [ ] `Curl.Conformance.UnitLibrary` and `Curl.Conformance.LibTest.UnitLibrary`: `Measure-CodeQuality.ps1 -Library` for each reports 0 failing members.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 041385d4 236db91c 4272a6b9 8738c7f6.
