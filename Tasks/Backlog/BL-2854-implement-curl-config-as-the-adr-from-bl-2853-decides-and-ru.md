---
id: BL-2854
title: Implement curl-config as the ADR from BL-2853 decides and run upstream cases 1013, 1014, 1022 and 1023
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2853]
touches: [Curl.slnx, Curl.Config, Curl.Config.UnitLibrary, Curl.Config.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamPerlCheckLine.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2854 — Implement curl-config as the ADR from BL-2853 decides and run upstream cases 1013, 1014, 1022 and 1023

## Goal

`curl-config` answers its options as the ADR from BL-2853 decides, and upstream cases 1013, 1014, 1022 and 1023 (which compare `curl --version` with `curl-config`) pass through the case runner and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

BL-2853 decided (ADR-0712): Curl ships `curl-config` on every platform as `Curl.Config` with `Curl.Config.UnitLibrary` and `.UnitTests`; implement its answer table and measure Linux and macOS option answers and the unknown-option text from the 8.22.0 tarball script. The four cases

## Acceptance criteria

- [ ] The executables ADR-0712 names (`Curl.Config`, `Curl.Config.UnitLibrary`, `Curl.Config.UnitTests`, assembly `curl-config`, shipped on every platform) exist, in `Curl.slnx` where the layout rules put it, with a `CLAUDE.md` that says what it holds, and answers `--version`, `--vernum`, `--protocols`, `--feature`, `--help` and the other options of the ADR exactly as the ADR lists (unit tests pin each answer, and an unknown option exits as upstream's `curl-config` does: measure the reference).
- [ ] `UpstreamCaseScreening` no longer skips cases 1013, 1014, 1022 and 1023, and a C# interpretation of `test1013.pl` and `test1022.pl` runs their postchecks; tests pin both.
- [ ] Cases 1013, 1014, 1022 and 1023 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] `curl --version` and `curl-config` agree on every platform's feature and protocol lists by a unit test that reads both from `Libcurl`'s `curl_version_info`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, each changed library keeps 100% line and branch coverage and complexity at most 10, and `dotnet publish` of the new executable still succeeds as native AOT if it is one.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
