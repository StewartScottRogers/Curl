---
id: BL-2889
title: Port upstream unit programs 1607 and 1663: CURLOPT_RESOLVE and interface option parsing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1607ResolveParsing.cs, Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1663InterfaceParsing.cs, Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1607ResolveParsingTests.cs, Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1663InterfaceParsingTests.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.UrlParsing.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Cli.UnitLibrary/InterfaceBinding.cs, Curl.Cli.UnitTests/CommandLineInterfaceAndLocalPortOptionTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2889 — Port upstream unit programs 1607 and 1663: CURLOPT_RESOLVE and interface option parsing

## Goal

Upstream cases 1607 (`CURLOPT_RESOLVE` parsing) and 1663 (interface option parsing, `Curl_parse_interface`) run as ported unit programs and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

Split from BL-2844 (2026-10-21), which ports the ten URL and address parsing unit programs and waits on this task. Follow ADR-0711: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/`, registered in `UnitPrograms.UrlParsing.cs`, reproducing every check of the C program (`https://github.com/curl/curl/tree/curl-8_22_0/tests/unit`) on the Curl type and returning `unitcheck.h`'s failure count, with an injectable function so a test can cover the failure branch. The Curl code under test is likely in Curl.Cli.UnitLibrary or Curl.Networking.UnitLibrary (the `--resolve` and `--interface` parsers). Where Curl's answer differs from the C table, measure real curl 8.22.0 first and fix Curl in its own library (add it to `touches`, with a focused test in its `.UnitTests`, ADR-0482); an internal type is reached through that library's `InternalsVisibleTo` for `Curl.Conformance.UnitProgram.UnitLibrary`. Run each case with `$env:CURL_UPSTREAM_CASE='<n>'`; `dotnet test Curl.Conformance.UnitTests --filter Name~NamedUpstreamCase`.

## Acceptance criteria

- [x] Each case named in the Goal has a port in `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.UrlParsing.cs`, with a test in `Curl.Conformance.UnitProgram.UnitTests/UrlParsing`; or the case has a Notes entry naming the C-only thing it tests.
- [x] Each ported case ends `Passed` with `CURL_UPSTREAM_CASE` set to it on Windows and is on `PassingUpstreamCases.txt`.
- [x] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- C sources read from `curl-8_22_0/tests/unit/unit1607.c` and `unit1663.c` (not vendored; fetched from GitHub).
- 1607 runs over the public `Curl.Networking.ResolveOverrides` (`Parse`, `Find`, `Entries[].IsPermanent`): all
  eleven rows matched Curl as it was. Curl always has IPv6, so the C program's `IPV6ONLY` skip never applies;
  its port check holds by construction because `Find` is keyed by the row's port.
- 1663 runs over the public `Curl.Cli.InterfaceBinding` (malformed = `CURLE_BAD_FUNCTION_ARGUMENT`). The `""`
  row found Curl wrong: `Parse("")` gave a plain name where `Curl_parse_interface` refuses it. Fixed in
  `InterfaceBinding.Parse` (empty is malformed) with a focused test in `Curl.Cli.UnitTests`. No command-line
  change: `--interface ''` is still refused as blank by `CommandLineOption.Text` before `Parse` is reached.
  Added `Curl.Cli.UnitLibrary/InterfaceBinding.cs` and its test file to `touches` for that fix; no task in Doing
  on `origin/work/dark-factory` named Curl.Cli.
- Chose `Curl.Cli.InterfaceBinding` over `Curl.Engine`'s internal `EngineInterfaceBinding` because Engine gives
  this library no `InternalsVisibleTo` and its csproj is outside `touches`.
- Measured: Curl.Cli.UnitLibrary 100/100, worst CRAP 10; Curl.Conformance.UnitProgram.UnitLibrary 100/100,
  worst CRAP 8 (after splitting `PermanenceProblem` out of `FirstProblem`, which was at complexity 16).

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 994c71ca 67867c61 aca7e1a2 dd1cc079.
- 2026-10-21: Doing -> Done. unit1607 (CURLOPT_RESOLVE parsing) and unit1663 (interface parsing) ported and on the ratchet; empty --interface value now malformed as Curl_parse_interface
