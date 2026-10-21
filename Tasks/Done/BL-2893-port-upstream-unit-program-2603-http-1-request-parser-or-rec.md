---
id: BL-2893
title: Port upstream unit program 2603 (HTTP/1 request parser) or record it has no counterpart
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2895]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Http1.cs, Curl.Conformance.UnitProgram.UnitLibrary/Headers/Http1, Curl.Conformance.UnitProgram.UnitTests/Headers/Http1, Documentation/Planning/Decisions/ADR-0711-upstream-unittest-cases-run-as-ported-unit-programs.md]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2893 — Port upstream unit program 2603 (HTTP/1 request parser) or record it has no counterpart

## Goal

Upstream case 2603 runs as a ported unit program and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or ADR-0711's row for 2603 records that Curl has no counterpart and why.

## Context

Split from BL-2846 for size. curl 8.22.0 `tests/unit/unit2603.c` tests `Curl_h1_req_parse_*`, libcurl's HTTP/1 *request* parser (used to turn a request into HTTP/2 or HTTP/3 frames and for proxies): it feeds request text in chunks and checks method, scheme, authority, path, header count and errors. ADR-0711's row says "port in BL-2846: the HTTP/1 response parser (`Curl.Protocol.Http.UnitLibrary`)", but the C program parses requests; a grep for a request parser outside the conformance stand-ins found none in production code. Decide whether Curl has a type with that observable behaviour (HTTP/2 or HTTP/3 request building from an HTTP/1 request, in `Curl.Protocol.Http.UnitLibrary`, `Curl.Http3.UnitLibrary` or `Curl.Engine.UnitLibrary`); port it if so, otherwise correct the ADR-0711 row with the C-only thing it tests. Mechanism: ADR-0711 and BL-2842's Hashes ports. Register in `UnitPrograms.Headers.Http1.cs`, which BL-2895 creates.

## Acceptance criteria

- [x] unit2603 is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Headers/Http1/`, check for check with the C program (named in its doc comment), registered in `UnitPrograms.Headers.Http1.cs`, with a test in `Curl.Conformance.UnitProgram.UnitTests/Headers/Http1/`; or this task's Notes name the C-only thing it tests and ADR-0711's row for 2603 says so.
- [x] If ported, the case ends `Passed` with `$env:CURL_UPSTREAM_CASE='2603'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Read curl 8.22.0 `tests/unit/unit2603.c` (raw from the `curl-8_22_0` tag). It drives `Curl_h1_req_parse_init/read/free`: an incremental parser fed request text in chunks (`"GET /path HTT"`, `"P/1.1\r\nHost: te"`, ...) that fills a `struct httpreq` (method, scheme, authority, path, `dynhds` headers) and reports how many input bytes it left unread. libcurl formats an HTTP/1 request and then reparses it with this to build HTTP/2 and HTTP/3 frames.
- Decision (Decided by Claude under Stewart's delegation, recorded in ADR-0711's row): no counterpart. The C-only thing it tests is `h1_req_parser`, the incremental, chunk-fed reparse of libcurl's own request text into a C request struct, with its unread-byte count, absolute-form (`GET ftp://ftp.curl.se/xxx?a=2`), authority-form (`CONNECT host:123`) and LF-only line handling. Curl never reparses request text from a stream: `Curl.Protocol.Http.UnitLibrary/Http2RequestHeaders.cs` `Of` turns the complete head that Curl formats itself into the HTTP/2 and HTTP/3 header list in one pass. A grep for a request parser found nothing else outside the conformance servers' stand-ins.
- The one behaviour the user can see is TEST7b (#19543): a custom method with a space (`-X "IN SANE"`) stays whole as `:method`. `Http2RequestHeaders.Of` splits the request line at every space, so Curl would send `:method IN`, `:path SANE /path`. Filed as BL-2915 (touches `Http2RequestHeaders.cs` and its tests). This task's `touches` does not cover that file.
- Criterion 2 does not apply, since nothing is ported. Criterion 3: `dotnet build Curl.slnx -warnaserror` gave 0 errors and the fast tests had 0 failures. No library code changed, so coverage and complexity are as before.
- ADR-0711: row 2603 now says no counterpart, and the counts line reads 63 ported and 21 no counterpart. `UnitPrograms.Headers.Http1.cs` stays empty. 2603 does not go on the ratchet.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 6cf104f8 41b7ad8f dd1cc079.
- 2026-10-21: Doing -> Done. ADR-0711 records 2603 as no counterpart (h1_req_parser, libcurl's incremental request-text reparse); the custom-method behaviour is filed as BL-2915
