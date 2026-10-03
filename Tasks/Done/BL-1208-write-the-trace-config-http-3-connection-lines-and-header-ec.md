---
id: BL-1208
title: Write the --trace-config http/3 connection lines and header echoes
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Quic.UnitLibrary, Curl.Quic.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1208 — Write the --trace-config http/3 connection lines and header echoes

## Goal

Under `-v --trace-config http/3` an HTTP/3 transfer also writes curl's QUIC connection lines and its per-header `status:` / `header:` echoes, as curl 8.18.0's ngtcp2 build does.

## Context

- Follow-up of BL-1168 (ADR-0375), which writes the stream lines (`end_headers`, `DATA len`, `ACK`, `CLOSED`, `quic close`) from `Http3StreamConnection` through `Http3StreamTrace`.
- Measured in BL-1168's Notes against `https://cloudflare-quic.com/`. Still unwritten:
  - connection lines: `handshake complete after <n>ms, remote transport[max_udp_payload=<n>, initial_max_data=<n>]`, `max bidi streams now <n>, used 0`, `peer verified`, `connect -> 0, done=1`, `peer idle timeout is <n>ms, set keep-alive to <n> ms.`, and at the end `[0] easy handle is done`, `no active streams, unset keep-alive`, `query conn[0]: MAX_CONCURRENT -> <n> (0 in use)`;
  - echoes: `[0] status: HTTP/3 200 ` followed by an empty line, then `[0] header: <name>: <value>` before each `<` header line. They interleave with the `<` lines, so they belong where the response head is parsed (compare BL-1205 for HTTP/2).
- The connection lines need the QUIC layer's transport parameters and handshake time; find where `IMultiplexedConnection` is made and how a trace sink can reach it.

## Acceptance criteria

- [x] Tests pin the connection lines above, in curl's places, under `--trace-config http/3`.
- [x] Tests pin the `status:` / `header:` echoes in curl's places among the `<` lines.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

**Measured** 2026-10-02 again with curl.se's Windows build 8.18.0 (ngtcp2 1.21.0) against
`https://cloudflare-quic.com/` (`-s -v --trace-config http/3 --http3-only`): the lines in the Context, in
those places, and the status echo's bytes are `status: HTTP/3 200 \r\n\n` - the traced text carries the
status line's CRLF. Each `header:` echo precedes its `<` line; the status echo follows it.

**Decision (ADR-0388):** `QuicDialer.WritesHttp3ConnectionLines` writes the handshake, bidi-limit,
`peer verified` and `connect` lines between the TLS lines and `Established`; the HTTP/3 session writes the
idle-timeout line as its first request stream opens (after `using HTTP/3`), from the new defaulted
`IMultiplexedConnection.PeerIdleTimeout`; `HttpResponseHeadReader.LineReporting` (before each `<` line)
carries the `header:` echo and `LineReported` the `status:` echo; a successful transfer ends with
`easy handle is done`, `no active streams, unset keep-alive` (none other in use) and
`query conn[<n>]: MAX_CONCURRENT -> <limit - opened> (<in use> in use)`. A failed transfer writes no end lines.

**Touches widened** to `Curl.Protocol.Abstractions.UnitLibrary` for `IMultiplexedConnection.PeerIdleTimeout`:
the Http library may not reference Quic, and the line must come after `using HTTP/3`, which the handler
writes. No task in Doing on `origin/work/dark-factory` named it (only BL-1208 itself was there).

`--ai-help` unchanged: no option changed.

Tests: `TcpConnectorQuicTests.ConnectMultiplexedAsync_WritingHttp3ConnectionLines_...`, `QuicConnectionTests.PeerIdleTimeout_*` (2),
`HttpProtocolHandlerTests.Http3Trace` (2 new, 2 updated with the echoes), `Http3StreamTraceTests` (1).

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --trace-config http/3 writes curl's QUIC connection lines, the idle-timeout and end-of-transfer lines, and status:/header: echoes among the < lines
