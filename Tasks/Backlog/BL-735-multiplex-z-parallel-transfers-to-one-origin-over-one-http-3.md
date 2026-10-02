---
id: BL-735
title: Multiplex -Z parallel transfers to one origin over one HTTP/3 connection
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-717, BL-732]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-735 — Multiplex -Z parallel transfers to one origin over one HTTP/3 connection

## Goal

With `-Z` and HTTP/3, transfers to the same origin share one QUIC connection as concurrent request streams, up to the server's `MAX_STREAMS`, as curl's official build multiplexes them, with `%{num_connects}` and the `-v` reuse lines as measured.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-717 (the multiplexing rules and pool changes made for HTTP/2) and BL-732 (HTTP/3 transfers).
- Measure with the official curl build against an HTTP/3 server through `Record-CurlExchange.ps1 -NoServer`: `-Z --http3 -v` for three URLs on one origin; stderr and `-w '%{num_connects}'` copied into Notes.

## Acceptance criteria

- [x] Measured first as above; copied into Notes.
- [ ] Tests with a fake multiplexed connection show three `-Z` transfers on one QUIC connection with client stream IDs 0, 4, 8, `%{num_connects}` as measured, and a new connection when `MAX_STREAMS` is reached.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-10-01, measurement: curl.se's 8.18.0 mingw build (LibreSSL, ngtcp2 1.21.0, nghttp3 1.15.0, WinGet) through
  `Record-CurlExchange.ps1 -NoServer` with `-Z --http3-only -v -s -o NUL -o NUL -o NUL -w '%{num_connects}\n'` and
  `https://cloudflare-quic.com/` three times (no local HTTP/3 server in the recorder; a public one stood in). Exit 0,
  stdout `1`, `0`, `0` (num_connects). stderr, certificate and header lines trimmed:
  - before the connection is up, for transfers 2 and 3 each: `* Connection #0 is not open enough, cannot reuse` /
    `* Found pending candidate for reuse and CURLOPT_PIPEWAIT is set` / `* Waiting on connection to negotiate possible multiplexing.`
    (the same three lines as HTTP/2, BL-717);
  - transfer 1: resolve, `* Established connection to H (A port 443) from L port P`, `* using HTTP/3`,
    `* [HTTP/3] [0] OPENED stream for https://H/`, its `[HTTP/3] [0] [...]` header lines, `* Request completely sent off`;
  - transfers 2 and 3: `* Multiplexed connection found` / `* Reusing existing https: connection with host H` /
    `* [HTTP/3] [4] OPENED stream ...` and then `[8]`;
  - one `* Connection #0 to host H:443 left intact`, after the last response.
- 2026-10-01, scope: QUIC connections are not pooled at all today - `PoolingConnector.ConnectMultiplexedAsync`
  (Curl.Networking.UnitLibrary) forwards every call to the inner connector, and its doc comment names keeping one
  per origin as BL-735's work. The `Multiplexed connection found` / PIPEWAIT / `MAX_CONCURRENT_STREAMS` / num_connects
  logic for HTTP/2 lives in that pool, so sharing an `Http3Session` between `-Z` transfers belongs there too, with a
  way for the pool to hold the session the handler builds over the QUIC connection (likely an addition to
  `IConnector`/`IConnectionSession` in Curl.Protocol.Abstractions.UnitLibrary). `touches` widened to
  Curl.Networking.UnitLibrary/.UnitTests and Curl.Protocol.Abstractions.UnitLibrary/.UnitTests for that.
  BL-824 (in Doing) touches Curl.Networking.UnitTests, so the task went back to Backlog until it is done.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Backlog. touches now include Curl.Networking.UnitTests (QUIC pooling lives in PoolingConnector), which BL-824 in Doing also touches; resume once BL-824 is Done
