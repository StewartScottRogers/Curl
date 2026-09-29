---
id: BL-731
title: Send an HTTP/3 request and read its response on a QUIC stream
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-730, BL-721, BL-658, BL-668]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Http3.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0169-http-3-requests-run-on-an-http3session-and-fall-back-to-tcp-only-when-quic-fails.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-731 — Send an HTTP/3 request and read its response on a QUIC stream

## Goal

When a transfer runs over HTTP/3, the HTTP handler sends the request as a QPACK HEADERS frame (pseudo-headers and header order as curl 8.21.0 sends them) and DATA frames on a new bidirectional QUIC stream, reads the response's HEADERS, DATA and trailers, and feeds the same output, `-i`/`-D`, `-f`, redirect, auth, cookie and progress paths as HTTP/1.1 and HTTP/2, with stream and connection errors mapped to exit 95 `CURLE_HTTP3` as BL-718's ADR states.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-730 (frames and QPACK), BL-721 (the multiplexed-connection contract the handler receives) and BL-658 (the HTTP/2 path, whose request/response plumbing this reuses). `Curl.Protocol.Http.UnitLibrary` gains a reference to `Curl.Http3.UnitLibrary` (allowed by BL-668); amend `Curl.Protocol.Http.UnitLibrary/CLAUDE.md` to name it.
- The handler never touches QUIC or UDP: it receives the multiplexed connection from the connector. Tests use a fake multiplexed connection replaying stream bytes.

## Acceptance criteria

- [x] `Curl.Protocol.Http.UnitTests` pin the HEADERS field section for a GET, a POST with `-d` and custom `-H` headers, and the output for a response with a body, with trailers, and with a stream reset mid-body (exit 18 once body bytes arrived, 95 before - `curl_ngtcp2.c`, see Notes), through the fake multiplexed connection.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan (decided by Claude, ADR-0169): `IHttpStreamSession` and `IHttpStreamConnection` now carry both HTTP/2 and HTTP/3. `Http3Session` wraps the connector's `IMultiplexedConnection` as the transfer's `IConnection` and opens curl's control and QPACK streams with the first request. `Http3StreamConnection` sends the QPACK HEADERS (the list `Http2RequestHeaders.Of` gives, because curl's h3 filter also calls `Curl_http_req_to_h2`) and the DATA frames, and presents the response as `HTTP/3 200 ` heads. `MultiplexedStreamAdapter` lets `Curl.Http3`'s readers work on a QUIC stream.
- Connect: `--http3-only` with a URL that is not `https://` is exit 3 before connecting (measured, ADR-0144). `--http3` tries QUIC, then TCP when it fails, and reports QUIC's failure when both fail. The timed happy-eyeballs race is BL-829. With a proxy, QUIC is not tried until BL-831 measures curl.
- Acceptance criterion corrected: it said a reset mid-body is exit 95, but `curl_ngtcp2.c` at curl-8_18_0 gives exit 18 once body bytes have arrived and 95 before. The tests pin both. Protocol violations are exit 56 `nghttp3_conn_read_stream returned error: <nghttp3 name>` (source, ADR-0169 section 6).
- The pinned GET field section `011b0000d1d750882f91d35d055c87a7c15f508825b650c3cb85e5c1dd` was checked by hand against the RFC 9204 static table and Huffman code. No local HTTP/3 server exists to measure it against.
- touches widened (no task in Doing names these): `Curl.Http3.UnitLibrary/CLAUDE.md`, which said request streams would land in that library and no longer did; the ADR-0169 file; the Decisions README.
- Follow-ups: BL-829 (race), BL-830 (server control and QPACK streams, GOAWAY), BL-831 (proxy), BL-832 (DATA over 16 MiB), BL-833 and BL-834 (which curl release to follow for reset handling, and the H3_REQUEST_REJECTED retry).
- Results: Curl.Protocol.Http.UnitTests 1247 passed. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line, 100% branch, 586 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. HTTP/3 requests go out as QPACK HEADERS and DATA on a QUIC stream and their responses feed the shared HTTP output paths, with curl's exit codes for resets and protocol errors
