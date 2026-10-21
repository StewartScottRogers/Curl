---
id: BL-2891
title: Port upstream unit programs 1620, 1627, 1653 and 1675: url.c, Curl_get_scheme, port parsing and the URL API
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2926]
touches: [Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1620UrlLoginDetails.cs, Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1627SchemeLookup.cs, Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1653PortParsing.cs, Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1675UrlApi.cs, Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1620UrlLoginDetailsTests.cs, Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1627SchemeLookupTests.cs, Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1653PortParsingTests.cs, Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1675UrlApiTests.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.UrlParsing.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlAuthority.cs, Curl.Protocol.Abstractions.UnitLibrary/Curl.Protocol.Abstractions.UnitLibrary.csproj, Curl.Protocol.Abstractions.UnitTests/CurlUrlAuthorityTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2891 — Port upstream unit programs 1620, 1627, 1653 and 1675: url.c, Curl_get_scheme, port parsing and the URL API

## Goal

Upstream cases 1620 (unit tests for `url.c`), 1627 (`Curl_get_scheme`), 1653 (urlapi port number parsing) and 1675 (URL API unit tests) run as ported unit programs and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

Split from BL-2844 (2026-10-21), which ports the ten URL and address parsing unit programs and waits on this task. Follow ADR-0711: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/`, registered in `UnitPrograms.UrlParsing.cs`, reproducing every check of the C program (`https://github.com/curl/curl/tree/curl-8_22_0/tests/unit`) on the Curl type and returning `unitcheck.h`'s failure count, with an injectable function so a test can cover the failure branch. The Curl code under test is likely in Curl.Protocol.Abstractions.UnitLibrary (`CurlUrlParser`), Curl.Core.UnitLibrary (`UrlSchemeGuesser`) and Curl.Api.UnitLibrary / Libcurl (the `curl_url_*` API). Where Curl's answer differs from the C table, measure real curl 8.22.0 first and fix Curl in its own library (add it to `touches`, with a focused test in its `.UnitTests`, ADR-0482); an internal type is reached through that library's `InternalsVisibleTo` for `Curl.Conformance.UnitProgram.UnitLibrary`. Run each case with `$env:CURL_UPSTREAM_CASE='<n>'`; `dotnet test Curl.Conformance.UnitTests --filter Name~NamedUpstreamCase`.

## Acceptance criteria

- [x] Each case named in the Goal has a port in `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.UrlParsing.cs`, with a test in `Curl.Conformance.UnitProgram.UnitTests/UrlParsing`; or the case has a Notes entry naming the C-only thing it tests.
- [x] Each ported case ends `Passed` with `CURL_UPSTREAM_CASE` set to it on Windows and is on `PassingUpstreamCases.txt`.
- [x] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 2026-10-21 (lane 5, first claim): 1620, 1627 and 1653 are ported and left uncommitted for the shift to stash
  and re-apply: `Unit1620UrlLoginDetails` (login rows over `CurlUrlAuthority.ParseLoginDetails`, handle rows
  over Libcurl's `CurlEasy`: perform with no URL is `UrlMalformat` twice, `Nobody` kept by `DupHandle`, `Reset`
  clears the URL; the magic-number and `Curl_init_transfer` checks are C memory bookkeeping and not ported),
  `Unit1627SchemeLookup` (transfer schemes from `CommandLineProtocolSet.KnownScheme`, proxy schemes from
  `CommandLineProxy.TryGetKind`; writes `547 invokes`), `Unit1653PortParsing` (over `CurlUrlAuthority.SplitPort`).
  All three were registered, their 33 port tests passed, and each named case ended `Passed` with
  `CURL_UPSTREAM_CASE` set. Not yet done: adding them to `PassingUpstreamCases.txt`, the full fast run and
  `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitProgram.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary`.
- Fix found by 1620 (ADR-0482): `Curl_parse_login_details` copies the options only when they have a length, so
  `:;` and `user:pw;` have no options; `CurlUrlAuthority` returned `""`. Fixed in `ParseLoginDetails`, with
  `Curl.Protocol.Abstractions.UnitTests/CurlUrlAuthorityTests.cs`. `SplitLogin` was renamed `ParseLoginDetails`
  and it and `SplitPort` made internal, and `Curl.Protocol.Abstractions.UnitLibrary.csproj` lets
  `Curl.Conformance.UnitProgram.UnitLibrary` see its internals, so those three files joined `touches` (no task
  in Doing named them).
- 1675 tests six urlapi.c helpers whose Curl counterparts are internal to Curl.Engine (`urlencode_str`) and
  Curl.Core (`Curl_url_same_origin`), outside this task's files, so it is split out as BL-2926 and this task
  waits on it. On the next claim: re-apply the stash, add 1620, 1627, 1653 (and 1675 from BL-2926) to the
  ratchet if not there, run the fast tests and the quality measure, commit.
- 2026-10-21 (lane 4, second claim): BL-2926 had already ported and listed 1675. Added 1620, 1627 and 1653 to
  `PassingUpstreamCases.txt`; fixed `CurlUrlAuthorityTests` for `Parse`'s new `refuseUnconvertibleName`
  parameter. `dotnet build Curl.slnx -warnaserror` clean, every fast test project green, and
  `Measure-CodeQuality.ps1` over Curl.Conformance.UnitProgram.UnitLibrary and Curl.Protocol.Abstractions.UnitLibrary
  reports 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 994c71ca 67867c61 aca7e1a2 dd1cc079.
- 2026-10-21: Doing -> Backlog. Waits on BL-2926 (port of unit1675, which needs Curl.Engine and Curl.Core internals); 1620, 1627 and 1653 are ported in the stash
- 2026-10-21: Backlog -> Doing. Criteria at claim: 994c71ca 67867c61 aca7e1a2 dd1cc079.
- 2026-10-21: Doing -> Done. 1620, 1627 and 1653 ported and on the ratchet; 1675 via BL-2926
