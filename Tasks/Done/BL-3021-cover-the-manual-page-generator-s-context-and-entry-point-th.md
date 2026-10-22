---
id: BL-3021
title: Cover the manual page generator's context and entry point, then measure Curl.Cli.UnitLibrary and Curl.Conformance.UnitLibrary quality
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3019, BL-3020]
touches: [Curl.Cli.UnitLibrary/ManualPages/ManualPageContext.cs, Curl.Cli.UnitLibrary/ManualPages/ManualPageGenerator.cs, Curl.Cli.UnitLibrary/ManualPages/ManualOptionIndex.cs, Curl.Cli.UnitTests/ManualPages/ManualPageContextTests.cs, Curl.Cli.UnitTests/ManualPages/ManualPageGeneratorTests.cs, Curl.Cli.UnitTests/ManualPages/ManualPageGeneratorBranchTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3021 — Cover the manual page generator's context and entry point, then measure Curl.Cli.UnitLibrary and Curl.Conformance.UnitLibrary quality

## Goal

`ManualPageContext` and `ManualPageGenerator` are covered on every line and branch, and `Curl.Cli.UnitLibrary` and `Curl.Conformance.UnitLibrary` are measured once with no member outside the four quality gates.

## Context

Split from BL-2881, the last of its coverage tasks (after BL-3019 and BL-3020). Cover in `ManualPageContextTests.cs` and `ManualPageGeneratorBranchTests.cs`, beside the existing `ManualPageGeneratorTests`: the `-c` option, `CURL_MAKETGZ_VERSION`, `%GLOBALS`, each `ManualPageExit` outcome, and the `listhelp`, `single`, `protos` and `listcats` commands that print the usage line. Make `ManualPageContext` and `ManualPageGenerator` members public where a test needs them (the types are documented already). Then run `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` and again with `Curl.Conformance.UnitLibrary` once each; close any gap in `ManualPages/` or `UpstreamManagenScript.cs` (add the file to `touches` in the same commit if it is not listed; a gap outside those folders goes in Notes and a separate task). Never change `CodeMetricsConfig.txt`.

## Acceptance criteria

- [x] Tests exist for each outcome listed above, each named after the behaviour it pins.
- [x] Measure-CodeQuality for `Curl.Cli.UnitLibrary` reports no member of `ManualPages/` outside the four gates, and for `Curl.Conformance.UnitLibrary` none of `UpstreamManagenScript`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Cli.UnitTests` and `Curl.Conformance.UnitTests`.

## Notes

- The first Measure-CodeQuality run for `Curl.Cli.UnitLibrary` found 100% line and branch coverage but four
  `ManualPages/` members over complexity 10: `ManualPageGenerator.Run` (22), `ListGlobals` (16), `MainPage` (14) and
  `ManualOptionIndex.Add` (14). They are split into short private methods (`ReadOptions`, `ApplyOption`,
  `GlobalName`, `HeaderLines`, `LongName`, `GlobalSeparator`, `WriteIndexLine`, and `ManualOptionIndex.HeaderLines`)
  with no change in behaviour. `ManualOptionIndex.cs` was added to `touches` for this; no other Doing task on
  `origin/work/dark-factory` names it.
- The final run measured both libraries together (`-Library Curl.Cli.UnitLibrary,Curl.Conformance.UnitLibrary`):
  0 failing members across 2 assemblies, so nothing in `UpstreamManagenScript.cs` needed changing.
- No member had to be made public: the tests drive `ManualPageGenerator.Run` and the already-public `ManualPageContext`.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: cd304d37 c61ae074 a9c88fc5.
- 2026-10-21: Doing -> Done. Generator and context covered on every outcome; Cli and Conformance libraries measure 0 failing members
