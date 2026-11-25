---
id: BL-5976
title: Reuse the connection of a timed-out HTTP/2 transfer for the next URL, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5974]
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptOutcome.cs, Curl.Original.Protocol.Http.UnitTests/Http2StreamAbortTests.cs, Curl.Original.Protocol.Http.UnitLibrary/ReadAheadConnectionStream.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Http.UnitTests/Http2SessionTests.cs, Curl.Original.Protocol.Http.UnitTests/ReadAheadConnectionStreamTests.cs, Curl.Original.Protocol.Http.UnitTests/Fakes/SessionHoldingConnection.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5976 — Reuse the connection of a timed-out HTTP/2 transfer for the next URL, as curl does

## Goal

After an HTTP/2 transfer ends by `-m`, Curl resets its stream and sends the next URL to the same origin on the same connection, as curl does.

## Context

Left by BL-5974 (ADR-1405), which writes `Connection #0 to host ... left intact` but still closes the connection. Measured with curl 8.18.0 (nghttp2), `--http2-prior-knowledge -v -m 2 <url>/x <url>/y` against `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H :status: 200','D hello\n'`: `RST_STREAM` 5 on stream 1, then `Reusing existing http: connection with host 127.0.0.1`, HEADERS on stream 3, `left intact` for #0 again, `RST_STREAM` 5 on stream 3, one GOAWAY. Curl dials connection #1 for `/y`. The cancelled read may have taken part of a frame (`Http2Session.ReadAndRouteFrameAsync`), so the session must not lose bytes. HTTP/3 still reports `closing connection` on a timeout (unmeasured; curl treats it as multiplexed too).

## Acceptance criteria

- [x] A timed-out HTTP/2 transfer sends `RST_STREAM` STREAM_CLOSED on its stream before the next transfer's HEADERS, pinned by a unit test.
- [x] The next transfer to the same origin goes out on the same connection, including when the timeout cut a frame in half, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision and reasons: ADR-1407. A frame is read only once `ReadAheadConnectionStream` holds it whole; the timed-out stream is reset and the connection marked reusable; a connection with a cancelled write (maybe part of a frame on the wire) takes no new stream and is closed.
- Both pinned by `Http2StreamAbortTests.ExecuteAsync_NextTransferAfterATimeout_GoesOutOnTheSameConnectionAfterTheReset` (cut between frames, in a header, in a payload).
- `touches` widened to the other files changed; no other task was in Doing on `origin/work/dark-factory`.
- `Http2Session.ReceiveAsync` now returns after a read that leaves the next frame partial (its callers already loop); `Http2SessionTests.ReceiveAsync_CancelledPartWayThroughAFrame_...` updated for it.
- Measured 2026-11-25: curl 8.18.0 and Curl, the Context's command plus `--trace-config http/2`, send the same frames (preface, HEADERS 1, RST_STREAM 1, HEADERS 3, RST_STREAM 3, one GOAWAY), with the RST trace line between the timeout line and `left intact`. Curl's HPACK bytes differ only by the port and user agent.
- `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: c10e1646 90bdb073 3d836d5e.
- 2026-11-25: Doing -> Done. A timed-out HTTP/2 transfer resets its stream and the next URL goes out on the same connection, as curl does
