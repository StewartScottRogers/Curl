---
id: BL-2846
title: Port upstream unit programs for headers, alt-svc, HSTS, netrc and HTTP/1 parsing: cases 1304, 1625, 1626, 1654, 1660, 1669, 1674, 2602, 2603 and 3400
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2841]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary, Curl.Conformance.UnitProgram.UnitTests]
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

## Log

- 2026-10-19: Created.
