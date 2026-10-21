---
id: BL-2844
title: Port upstream unit programs for URL and address parsing: cases 1395, 1607, 1614, 1620, 1627, 1653, 1663, 1664, 1675 and 1961
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2886, BL-2889, BL-2890, BL-2891, BL-2892]
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlDotSegments.cs, Curl.Protocol.Abstractions.UnitLibrary/Curl.Protocol.Abstractions.UnitLibrary.csproj, Curl.Protocol.Abstractions.UnitTests/CurlUrlDotSegmentsTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.UrlParsing.cs, Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing, Curl.Conformance.UnitProgram.UnitTests/UrlParsing]
requirement: none
created: 2026-10-19
completed:
---
# BL-2844 — Port upstream unit programs for URL and address parsing: cases 1395, 1607, 1614, 1620, 1627, 1653, 1663, 1664, 1675 and 1961

## Goal

Upstream cases 1395, 1607, 1614, 1620, 1627, 1653, 1663, 1664, 1675 and 1961 run as ported unit programs over Curl's URL, address and string-parsing code and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

These ten `unittest` cases (skipped today with `Curl lacks the feature unittest`; gap scorecard 2026-10-19_1329: `excluded | debug-build-only`) test one area each in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/unit): 1395 `Curl_dedotdotify`, 1607 `CURLOPT_RESOLVE` parsing, 1614 noproxy and CIDR comparisons (feature `proxy`), 1620 unit tests for `url.c`, 1627 `Curl_get_scheme`, 1653 urlapi port number parsing, 1663 interface option parsing, 1664 `strparse.c` string parsing (feature `large-size`), 1675 URL API unit tests, 1961 `curlx_inet_ntop()` and `curlx_inet_pton()`. Curl's URL model is its own (ADR-0010 explains why `System.Uri` is not used); the parsers sit in `Curl.Core.UnitLibrary`, `Curl.Networking.UnitLibrary` and `Curl.Cli.UnitLibrary` (find each type with the Glossary and `rg`). Follow the ADR from BL-2840 and the mechanism from BL-2841: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary`, registered in `UnitPrograms.Ported`, reproducing every check of the C program on the Curl type and returning its failure count. Where Curl's answer differs from the C function's table, measure real curl 8.22.0 first (`Record-CurlExchange.ps1`; match the platform's build) and fix Curl if it is wrong.

## Acceptance criteria

- [ ] Each of the ten cases has a port in `Curl.Conformance.UnitProgram.UnitLibrary` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.Ported`, with a test in `Curl.Conformance.UnitProgram.UnitTests`; or the case has a Notes entry naming the C-only thing it tests and the ADR from BL-2840 row says so.
- [ ] Each ported case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to `touches` in the same commit.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library and the new library keep 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

Ports go in Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/ and their tests in Curl.Conformance.UnitProgram.UnitTests/UrlParsing/; register them only in Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.UrlParsing.cs, which BL-2886 creates. Its project references are added by BL-2886; if one is missing, add it in a separate one-line commit and say so in the Log (Stewart, 2026-10-21: keep the seven ports parallel).

2026-10-21, lane 5 (first claim): split for size. Ten C programs, each needing its C source read, its Curl counterpart found and possibly a fix in another library, do not fit one run, and the claim fingerprints the criteria, so they cannot be narrowed to the cases done here. The other nine are filed as BL-2889 (1607, 1663), BL-2890 (1614), BL-2891 (1620, 1627, 1653, 1675) and BL-2892 (1664, 1961), each with its own criteria, and this task now waits on them. Prerequisites checked: the case runner runs a ported `unittest` case (1395 passed with `CURL_UPSTREAM_CASE=1395`), and all ten cases are vendored in `UpstreamTestData`.

Case 1395 is done and left uncommitted (rule 6), so the shift stashes it for the next claim: `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlDotSegments.cs` is now a row-for-row port of curl 8.22.0's `dedotdotify` and `needs_dedotdot` (`Dedotdotify` returns null when there is nothing to remove and handles the leading `./` and `../` rules A and D; `Remove` keeps its old answer for every parsed path), with `CurlUrlDotSegmentsTests.cs`; the Abstractions csproj gives `InternalsVisibleTo` to `Curl.Conformance.UnitProgram.UnitLibrary`; `UrlParsing/Unit1395DotSegmentRemoval.cs` ports all 106 `pairs` rows (failure count = mismatches + 1 for the closing `fail_if`), with `UrlParsing/Unit1395DotSegmentRemovalTests.cs`, and is registered in `UnitPrograms.UrlParsing.cs`. Touches now name the three Abstractions files (no task in Doing touched them). When the stash is applied after BL-2889 to BL-2892 land, `UnitPrograms.UrlParsing.cs` will conflict: keep every program, adding `new Unit1395DotSegmentRemoval()`. Then add `1395` to `PassingUpstreamCases.txt` (not yet added), build, run the fast tests and `Measure-CodeQuality.ps1` for Curl.Protocol.Abstractions.UnitLibrary and Curl.Conformance.UnitProgram.UnitLibrary (not yet run).

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 759c7e24 2e3e75ab e66ae9dd c0723528 620ebead.
- 2026-10-21: Doing -> Backlog. Split for size: waits on BL-2889, BL-2890, BL-2891 and BL-2892 for the other nine cases; case 1395 is done and stashed.
