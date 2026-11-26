---
id: BL-5983
title: Measure and match curl's connection end when -f fails an HTTP/3 transfer
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5981]
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptOutcome.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitTests/Http3StreamAbortTests.cs, Record-CurlExchange.ps1, Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5983 — Measure and match curl's connection end when -f fails an HTTP/3 transfer

## Goal

When `-f` fails an HTTP/3 transfer, Curl writes the connection line, ends the stream and treats the next URL as curl does.

## Context

BL-5978 (ADR-1410) matched `-m` over HTTP/3 and left `-f` as it was: Curl writes `closing connection`, sends nothing for the stream and dials again. BL-5977 (ADR-1408) matched `-f` over HTTP/2. The recorder's HTTP/3 origin (BL-5981) answers 200 only; extend it to answer another status.

## Acceptance criteria

- [x] The Notes record what real curl writes and sends when `-f` fails an HTTP/3 transfer with a second URL to the same origin.
- [x] Curl's connection line, stream ending and connection reuse match that measurement, pinned by unit tests.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 (ngtcp2, nghttp3, WinGet), `Record-CurlExchange.ps1 -Http3StalledOrigin`, `--http3-only -k -sS -v --trace-config http/3 -f -m 5 <url>/x <url>/y`, both answered `500`, exit 22. Every case: `easy handle is done`, `no active streams, unset keep-alive`, `MAX_CONCURRENT -> 99`, `Connection #0 to host ... left intact`, `Reusing existing https: connection`, second request on stream 4.
- Stream left open (head only, or head and DATA): STOP_SENDING 0x10c and decoder bytes `40` (`44` for stream 4); the next transfer writes `[0] quic close(app_error=268) -> 0`.
- Stream ended (by the head, or by DATA): nothing sent; `[0] CLOSED`, `[0] quic close(app_error=256) -> 0`.
- Curl.Console run against the same four cases gave the same connection lines, wire bytes and close lines. Not matched, and outside this task: curl's `error 22 writing 2 bytes of headers`, `read_stream`, `xfer write failed` and `cf_recv -> 22` trace lines (ADR-1414).
- Decision (ADR-1414): Curl first reads what has already arrived of the failed response, without waiting, so an ended stream is not cancelled.
- Recorder: the HTTP/3 origin now answers with the first `-Response` status; `-Http3EndStream` ends the stream.
- Added to touches: `Http3StreamConnection.cs` (the read of arrived frames) and the library's `CLAUDE.md`; no other task in Doing names them.
- Quality: Curl.Original.Protocol.Http.UnitLibrary 100% line, 100% branch, 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: fb48a309 9289376a 3d836d5e.
- 2026-11-25: Doing -> Done. curl measured and matched over HTTP/3 for -f (ADR-1414)
