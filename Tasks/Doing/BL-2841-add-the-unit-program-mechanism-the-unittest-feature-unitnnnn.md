---
id: BL-2841
title: Add the unit-program mechanism: the unittest feature, unitNNNN tools and the table of ported unit programs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2840]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.slnx, Curl.Conformance.UnitProgram.UnitLibrary, Curl.Conformance.UnitProgram.UnitTests, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2841 — Add the unit-program mechanism: the unittest feature, unitNNNN tools and the table of ported unit programs

## Goal

The upstream case runner runs a `unittest` case through a ported unit program when one is registered, exactly as it runs a ported libtest program, and skips it with a clear reason when none is, with the new project and its table in place for the port tasks BL-2842 to BL-2848.

## Context

BL-2840's ADR decides the mechanism, the project names and the classification; read it first and follow it. The proposal it starts from: new projects `Curl.Conformance.UnitProgram.UnitLibrary` (namespace `Curl.Conformance.UnitProgram`) and `Curl.Conformance.UnitProgram.UnitTests`, added to `Curl.slnx` in the flat alphabetical run (each `.UnitTests` directly after its library); an `IUnitProgram` with `ProgramName` (`unit1302`, `tool1394`) and `RunAsync` modelled on `Curl.Conformance.LibTest.UnitLibrary`'s `ILibTest`, `LibTestContext`, `LibTestResult` and `LibTestPrograms` (read its `CLAUDE.md`); an explicit table `UnitPrograms.Ported` (no reflection scan, AOT) that each port task appends to.

In the runner (`Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs`, `UpstreamCaseRunner.cs`): a case with the `unittest` feature and no registered port is skipped with a reason that names the program (the pattern of `LibTestNotPorted`); a case whose program is registered is run, and its exit code and standard output are verified like a libtest program's. `UpstreamCurlPlatform` must report `unittest` to a case only through the registered table, not as a blanket platform feature. Nothing is ported here beyond one trivial program in the new `.UnitTests` project that proves the path end to end; BL-2842 onward port the real programs.

## Acceptance criteria

- [ ] `Curl.slnx` lists both new projects in the flat alphabetical run, and both obey the repository's layout, naming, nullable, warnings-as-errors, AOT and quality-gate rules (`Directory.Build.props` applies).
- [ ] `UnitPrograms.Ported` is an explicit list; a unit test fails if two programs share a `ProgramName`.
- [ ] `UpstreamCaseScreening` tests show: a `unittest` case with no registered port is skipped with a reason naming `unit<n>` or `tool<n>`; with a registered port it is not skipped for `unittest`; a non-`unittest` case is unaffected. Another screening reason still wins where it applies.
- [ ] A runner test runs a registered fake unit program through `UpstreamCaseRunner` and verifies its standard output and exit code against an inline case; a program returning a non-zero failure count fails the case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and the new library and `Curl.Conformance.UnitLibrary` keep 100% line and branch coverage and complexity at most 10; the new project has its own `CLAUDE.md` that says what it holds.
- [ ] Every `unittest` case stays off `PassingUpstreamCases.txt` (this task adds none), so the ratchet is unchanged.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b5f01ec6 81432b77 c5c245cb 59f1e430 e8a42364 2bb4cd17.
