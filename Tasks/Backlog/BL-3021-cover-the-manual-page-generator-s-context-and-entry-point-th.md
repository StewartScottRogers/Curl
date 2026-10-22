---
id: BL-3021
title: Cover the manual page generator's context and entry point, then measure Curl.Cli.UnitLibrary and Curl.Conformance.UnitLibrary quality
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3019, BL-3020]
touches: [Curl.Cli.UnitLibrary/ManualPages/ManualPageContext.cs, Curl.Cli.UnitLibrary/ManualPages/ManualPageGenerator.cs, Curl.Cli.UnitTests/ManualPages/ManualPageContextTests.cs, Curl.Cli.UnitTests/ManualPages/ManualPageGeneratorTests.cs, Curl.Cli.UnitTests/ManualPages/ManualPageGeneratorBranchTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-3021 — Cover the manual page generator's context and entry point, then measure Curl.Cli.UnitLibrary and Curl.Conformance.UnitLibrary quality

## Goal

`ManualPageContext` and `ManualPageGenerator` are covered on every line and branch, and `Curl.Cli.UnitLibrary` and `Curl.Conformance.UnitLibrary` are measured once with no member outside the four quality gates.

## Context

Split from BL-2881, the last of its coverage tasks (after BL-3019 and BL-3020). Cover in `ManualPageContextTests.cs` and `ManualPageGeneratorBranchTests.cs`, beside the existing `ManualPageGeneratorTests`: the `-c` option, `CURL_MAKETGZ_VERSION`, `%GLOBALS`, each `ManualPageExit` outcome, and the `listhelp`, `single`, `protos` and `listcats` commands that print the usage line. Make `ManualPageContext` and `ManualPageGenerator` members public where a test needs them (the types are documented already). Then run `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` and again with `Curl.Conformance.UnitLibrary` once each; close any gap in `ManualPages/` or `UpstreamManagenScript.cs` (add the file to `touches` in the same commit if it is not listed; a gap outside those folders goes in Notes and a separate task). Never change `CodeMetricsConfig.txt`.

## Acceptance criteria

- [ ] Tests exist for each outcome listed above, each named after the behaviour it pins.
- [ ] Measure-CodeQuality for `Curl.Cli.UnitLibrary` reports no member of `ManualPages/` outside the four gates, and for `Curl.Conformance.UnitLibrary` none of `UpstreamManagenScript`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Cli.UnitTests` and `Curl.Conformance.UnitTests`.

## Notes

## Log

- 2026-10-21: Created.
