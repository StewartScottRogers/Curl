---
id: BL-5978
title: Measure and match curl's connection end when -m ends an HTTP/3 transfer
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptOutcome.cs, Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Http3StreamAbortTests.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitTests/Fakes/FakeMultiplexedStream.cs, Curl.Http3.UnitLibrary/Http3ClientRequestStream.cs, Curl.Http3.UnitLibrary/Http3ClientSession.cs, Curl.Http3.UnitLibrary/QpackDecoder.cs, Curl.Http3.UnitTests/Http3ClientSessionTests.cs, Curl.Http3.UnitTests/QpackDecoderTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5978 — Measure and match curl's connection end when -m ends an HTTP/3 transfer

## Goal

When `-m` ends an HTTP/3 transfer, Curl writes the connection line, ends the stream and treats the next URL as curl does.

## Context

BL-5974 (ADR-1405) and BL-5976 (ADR-1407) matched curl for HTTP/2: the stream is reset, `-v` says `left intact` and the next URL reuses the connection. HTTP/3 was left unchanged because its timeout was never measured; Curl still writes `closing connection`. curl treats HTTP/3 as multiplexed too. Measure with an HTTP/3 curl build and `Record-CurlExchange.ps1`, extending it if it cannot serve a stalled HTTP/3 stream.

## Acceptance criteria

- [x] The task's Notes record what real curl writes and sends when `-m` ends an HTTP/3 transfer with a second URL to the same origin.
- [x] Curl's connection line, stream ending and connection reuse match that measurement, pinned by unit tests.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Measured with curl 8.18.0 (ngtcp2, nghttp3, WinGet) against a stalled System.Net.Quic origin,
`--http3-only -k -v --trace-config http/3 -m 2 <url>/x <url>/y`, exit 28:

- writes `Operation timed out after 2015 milliseconds with 6 bytes received`,
  `[HTTP/3] [0] easy handle is done`, `no active streams, unset keep-alive`,
  `query conn[0]: MAX_CONCURRENT -> 99 (0 in use)`,
  `Connection #0 to host 127.0.0.1:18443 left intact`, then
  `Reusing existing https: connection with host 127.0.0.1` and the second request on stream 4;
- sends STOP_SENDING 0x10c on stream 0 and `40` on the QPACK decoder stream (`44` for stream 4).

Curl run against the same origin (`-Curl Curl.Console\bin\Debug\net10.0\curl.exe`) gave the same
lines, abort codes and decoder bytes. Decision: ADR-1409.

- Touches widened: `ResponseExchange.cs` calls the new stream end, and the Stream Cancellation
  needed `Curl.Http3.UnitLibrary`. No task in Doing named any of these.
- `Record-CurlExchange.ps1` was extended (`-Http3StalledOrigin`) to measure. BL-5973 entered
  Doing during this run holding that script, so the extension is not committed here: it is a
  patch in BL-5981. No criterion needs it, so this task is not requeued.
- Quality: both changed libraries 100% line and branch, no failing member.
- Fast tests: one `Curl.Networking.UnitTests` test failed in the full run and none when that
  project ran alone: BL-5980.
- Follow-ups: BL-5983 (`-f` over HTTP/3), BL-5982 (`quic close` trace line).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 24b1db49 9289376a 3d836d5e.
- 2026-11-25: Doing -> Done. Curl cancels the stream of a timed-out HTTP/3 transfer and reuses its connection, as curl 8.18.0 does
