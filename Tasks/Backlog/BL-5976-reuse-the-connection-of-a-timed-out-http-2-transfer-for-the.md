---
id: BL-5976
title: Reuse the connection of a timed-out HTTP/2 transfer for the next URL, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5974]
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptOutcome.cs, Curl.Original.Protocol.Http.UnitTests/Http2StreamAbortTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5976 — Reuse the connection of a timed-out HTTP/2 transfer for the next URL, as curl does

## Goal

After an HTTP/2 transfer ends by `-m`, Curl resets its stream and sends the next URL to the same origin on the same connection, as curl does.

## Context

Left by BL-5974 (ADR-1405), which writes `Connection #0 to host ... left intact` but still closes the connection. Measured with curl 8.18.0 (nghttp2), `--http2-prior-knowledge -v -m 2 <url>/x <url>/y` against `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H :status: 200','D hello\n'`: `RST_STREAM` 5 on stream 1, then `Reusing existing http: connection with host 127.0.0.1`, HEADERS on stream 3, `left intact` for #0 again, `RST_STREAM` 5 on stream 3, one GOAWAY. Curl dials connection #1 for `/y`. The cancelled read may have taken part of a frame (`Http2Session.ReadAndRouteFrameAsync`), so the session must not lose bytes. HTTP/3 still reports `closing connection` on a timeout (unmeasured; curl treats it as multiplexed too).

## Acceptance criteria

- [ ] A timed-out HTTP/2 transfer sends `RST_STREAM` STREAM_CLOSED on its stream before the next transfer's HEADERS, pinned by a unit test.
- [ ] The next transfer to the same origin goes out on the same connection, including when the timeout cut a frame in half, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
