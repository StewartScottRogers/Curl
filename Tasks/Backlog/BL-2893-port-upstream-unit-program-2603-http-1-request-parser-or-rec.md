---
id: BL-2893
title: Port upstream unit program 2603 (HTTP/1 request parser) or record it has no counterpart
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2889]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Http1.cs, Curl.Conformance.UnitProgram.UnitLibrary/Headers/Http1, Curl.Conformance.UnitProgram.UnitTests/Headers/Http1, Documentation/Planning/Decisions/ADR-0711-upstream-unittest-cases-run-as-ported-unit-programs.md]
requirement: none
created: 2026-10-21
completed:
---
# BL-2893 — Port upstream unit program 2603 (HTTP/1 request parser) or record it has no counterpart

## Goal

Upstream case 2603 runs as a ported unit program and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or ADR-0711's row for 2603 records that Curl has no counterpart and why.

## Context

Split from BL-2846 for size. curl 8.22.0 `tests/unit/unit2603.c` tests `Curl_h1_req_parse_*`, libcurl's HTTP/1 *request* parser (used to turn a request into HTTP/2 or HTTP/3 frames and for proxies): it feeds request text in chunks and checks method, scheme, authority, path, header count and errors. ADR-0711's row says "port in BL-2846: the HTTP/1 response parser (`Curl.Protocol.Http.UnitLibrary`)", but the C program parses requests; a grep for a request parser outside the conformance stand-ins found none in production code. Decide whether Curl has a type with that observable behaviour (HTTP/2 or HTTP/3 request building from an HTTP/1 request, in `Curl.Protocol.Http.UnitLibrary`, `Curl.Http3.UnitLibrary` or `Curl.Engine.UnitLibrary`); port it if so, otherwise correct the ADR-0711 row with the C-only thing it tests. Mechanism: ADR-0711 and BL-2842's Hashes ports. Register in `UnitPrograms.Headers.Http1.cs`, which BL-2889 creates.

## Acceptance criteria

- [ ] unit2603 is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Headers/Http1/`, check for check with the C program (named in its doc comment), registered in `UnitPrograms.Headers.Http1.cs`, with a test in `Curl.Conformance.UnitProgram.UnitTests/Headers/Http1/`; or this task's Notes name the C-only thing it tests and ADR-0711's row for 2603 says so.
- [ ] If ported, the case ends `Passed` with `$env:CURL_UPSTREAM_CASE='2603'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
