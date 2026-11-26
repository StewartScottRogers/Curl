---
id: BL-6038
title: Send --doh-url's A and AAAA queries on one HTTP/2 connection, as curl multiplexes them
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.Http2.cs, Curl.Networking.UnitLibrary/DohDnsResolver.Exchange.cs, Curl.Networking.UnitTests/DohDnsResolverHttp2Tests.cs, Curl.Networking.UnitLibrary/DohDnsResolver.SubTransferTrace.cs, Curl.Networking.UnitLibrary/DohPipeWaitTraceEvents.cs, Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6038 — Send --doh-url's A and AAAA queries on one HTTP/2 connection, as curl multiplexes them

## Goal

Under `--doh-url` over HTTP/2, the A and AAAA queries share one connection as two streams, with the connection count and `-v` lines curl prints.

## Context

ADR-1424 (BL-6015) gave `--doh-url` HTTP/2 with a connection for each query. curl sets `CURLOPT_PIPEWAIT` on its DoH sub-transfers (`lib/doh.c`) and can multiplex them. Measure curl 8.18.0 on Ubuntu against an HTTP/2 DoH server (Unbound, `--trace-config doh`) first.

## Acceptance criteria

- [x] A unit test in `DohDnsResolverHttp2Tests` shows the connection count and streams curl was measured to use for the A and AAAA queries, and pins the `-v` lines measured.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 on Ubuntu (WSL) against Unbound 1.26.2 DoH in Docker; the results and the
  decision are in ADR-1428.
- Touches widened: the query runner (`SubTransferTrace.cs`), the new `DohPipeWaitTraceEvents.cs`,
  and the two documents saying each query has its own connection. No task in Doing named them.
- Connection reuse for later names and HTTP/1.1 (also measured) is BL-6051.
- Checks: `dotnet build` 0 warnings; whole fast suite 58490 passed, 0 failed; `Measure-CodeQuality.ps1 -Library
  Curl.Networking.UnitLibrary` 0 failing members. After those runs one line of defence was added (a reset
  of a stream already given up on is ignored, tested by a new data row) and Curl.Networking.UnitTests
  rebuilt and rerun (4775 passed). Platform: the new tests use only in-memory fakes.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: f797e6d2 3d836d5e.
- 2026-11-26: Doing -> Done. --doh-url over HTTP/2 sends the A and AAAA queries as streams 1 and 3 of one connection, with curl's -v lines (ADR-1428).
