---
id: BL-2846
title: Port upstream unit programs for headers, alt-svc, HSTS, netrc and HTTP/1 parsing: cases 1304, 1625, 1626, 1654, 1660, 1669, 1674, 2602, 2603 and 3400
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2886, BL-2895, BL-2896, BL-2897, BL-2898, BL-2893, BL-2894]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.cs, Curl.Conformance.UnitProgram.UnitLibrary/Headers, Curl.Conformance.UnitProgram.UnitTests/Headers]
requirement: none
created: 2026-10-19
completed:
---
# BL-2846 — Port upstream unit programs for headers, alt-svc, HSTS, netrc and HTTP/1 parsing: cases 1304, 1625, 1626, 1654, 1660, 1669, 1674, 2602, 2603 and 3400

## Goal

Upstream cases 1304, 1625, 1626, 1654, 1660, 1669, 1674, 2602, 2603 and 3400 run as ported unit programs over Curl's netrc, header, alt-svc, HSTS, HTTP/1 and capsule code and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

These ten `unittest` cases (skipped today with `Curl lacks the feature unittest`; gap scorecard 2026-10-19_1329: `excluded | debug-build-only`) test one area each in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/unit): 1304 netrc parsing (feature `netrc`), 1625 `Curl_compareheader` and 1626 `Curl_copy_header_value` (feature `http`), 1654 alt-svc (sets `CURL_TIME=1548369261`) and 1669 alt-svc load of more than 5000 entries, 1660 HSTS and 1674 HSTS load of more than 10000 entries (both `CURL_TIME=1548369261`), 2602 `dynhds` (the header list), 2603 the HTTP/1 parser and 3400 the capsule protocol encoder and decoder. Curl's counterparts are the alt-svc and HSTS stores, the netrc reader, the HTTP/1 parser and header code (`Curl.Protocol.Http.UnitLibrary`, `Curl.Cli.UnitLibrary`, `Curl.Core.UnitLibrary`) and the capsule code (`Curl.Http3.UnitLibrary`); find each with the Glossary and `rg`. The fixed clock `CURL_TIME` is an injected `TimeProvider` in the port (never `Thread.Sleep`). `dynhds` and the C parser's buffer states (2602, 2603) may have no counterpart; judge by whether Curl has a type with that observable behaviour. Follow the ADR from BL-2840 and the mechanism from BL-2841: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary`, registered in `UnitPrograms.Ported`, reproducing every check of the C program and returning its failure count.

## Acceptance criteria

- [ ] Each of the ten cases has a port in `Curl.Conformance.UnitProgram.UnitLibrary` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.Ported`, with a test in `Curl.Conformance.UnitProgram.UnitTests`; or the case has a Notes entry naming the C-only thing it tests and the ADR from BL-2840 row says so.
- [ ] Each ported case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to `touches` in the same commit.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library and the new library keep 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

Ports go in Curl.Conformance.UnitProgram.UnitLibrary/Headers/ and their tests in Curl.Conformance.UnitProgram.UnitTests/Headers/; register them only in Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.cs, which BL-2886 creates. Its project references are added by BL-2886; if one is missing, add it in a separate one-line commit and say so in the Log (Stewart, 2026-10-21: keep the seven ports parallel).

2026-10-21, lane 6, split for size before any code: nine C programs (about 1,400 lines) over five Curl subsystems, with file-backed stores, a fixed clock, a missing alt-svc 5000-entry cap and two parsers whose counterparts need finding, is more than one run. Subtasks, each with disjoint `touches` so they run in parallel once BL-2895 lands:
- BL-2895 (High): 1625, 1626, and splits `UnitPrograms.Headers.cs` into six `UnitPrograms.Headers.<Subarea>.cs` files so the others never touch one file.
- BL-2896: 1304 netrc. BL-2897: 1654, 1669 alt-svc (likely adds the 5000-entry cap to `AltSvcCache`). BL-2898: 1660, 1674 HSTS. BL-2893: 2603, which tests libcurl's HTTP/1 *request* parser, not the response parser ADR-0711's row names; port or correct the row. BL-2894: 3400 capsules over `CapsuleDatagramChannel`.
Prerequisite pass: the runner already runs ported unit programs (1302 and 1600 pass on the ratchet), passes `%LOGDIR` paths as arguments and writes `<client><file>`; `UnitProgramContext` carries no environment, so `CURL_TIME` is a fixed injected `TimeProvider` in the ports. `NetrcFile`, `AltSvcCache`, `HstsCache` (with `MaxEntries = 10_000`) and `CapsuleDatagramChannel` exist; no Curl type is named after `Curl_compareheader` or `Curl_copy_header_value` (BL-2895 finds the HTTP code that does that).

2602: no port. It tests `dynhds`, libcurl's growable array of `dynhds_entry` structs (`Curl_dynhds_add`, `_cadd`, `_h1_cadd_line`, `_count`, `_getn`, its entry and size limits); Curl keeps headers in .NET collections, so there is no type with that observable behaviour. ADR-0711's row for 2602 already says "no counterpart (BL-2846 records it)"; when this task returns after its subtasks it confirms the ten cases and ticks the criteria.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 759c7e24 2e3e75ab e66ae9dd c0723528 620ebead.
- 2026-10-21: Doing -> Backlog. Split for size into BL-2895 to BL-2894 (one port group each, disjoint touches); waits on them
- 2026-10-21: Backlog -> Doing. Criteria at claim: 759c7e24 2e3e75ab e66ae9dd c0723528 620ebead.
