---
id: BL-5941
title: Report curl's { [0 bytes data] -v line when a -I HTTP/2 response ends at a DATA frame
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpStreamOpenedLines.cs, Curl.Original.Protocol.Http.UnitTests/Http2HeadResponseWithDataTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5941 — Report curl's { [0 bytes data] -v line when a -I HTTP/2 response ends at a DATA frame

## Goal

`curl -v -I` over HTTP/2, answered with a head and then a DATA frame, writes the same `-v` data lines as curl.

## Context

Found in BL-5938. `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames 'H :status: 200|content-length: 3','D no\n'` with `--http2-prior-knowledge -sS -v -I -m 3`: curl 8.18.0 (nghttp2, run through WSL) writes `{ [0 bytes data]` after `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR` and before `* shutting down connection #0`; Curl writes no such line. curl writes it too when the DATA is empty and ends the stream (`'D! '`). Measure `-I` over HTTP/1.1 and with stderr to a terminal before deciding when it appears. See `Http2StreamConnection.EndAtUnwantedBodyAsync`.

## Acceptance criteria

- [x] A unit test on the transfer's recorded events shows the `-v` data line curl writes for both measured cases.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured, curl 8.18.0 (nghttp2, WSL), `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -ListenAddress 172.26.96.1 -Curl wsl.exe`, `-sS -v -I`: a DATA frame that ends the response gets `{ [0 bytes data]` when its data is past the promised length (none for HEAD; after the `nghttp2 shuts down` line) or is empty (`D ` and `D! `). `-I -X GET` with DATA within `content-length: 3`, or short of `content-length: 9`, gets `{ [3 bytes data]`. A followed 301 (`-I -L`) writes no data line for the ignored hop.
- `-I` over HTTP/1.1 writes no data line for a bare head, and `{ [3 bytes data]` when body bytes came with it, which Curl already did. With standard output a terminal (WSL `script`) curl writes no data line; `VerboseTransferEventWriter` already hides it, so the stream only reports the event.
- So `Http2StreamConnection.EndAtUnwantedBodyAsync` reports the frame's data, empty when past the promised length, and nothing while `AbandonResponseAsync` reads the body. Matches measured curl, so no new ADR (ADR-1384 covers the shutdown).
- `touches` widened to `HttpStreamOpenedLines.cs` (the event sink the stream has); no task in Doing named it.
- End-to-end with Curl.Console, the DATA cases (`D no\n`, `D! `, `-I -X GET`) match curl's lines.
- Filed the gaps found beside it: BL-5946 (no-body HTTP/2 responses without that DATA path), BL-5947 (`-I -X GET` exit 16), BL-5948 (`-I` HTTP/1.1 with body bytes shuts the connection down).
- Fast tests: 58079 passed, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 54860144 3d836d5e.
- 2026-11-25: Doing -> Done. curl -v -I over HTTP/2 reports the data line of the DATA frame that ends the response, as curl does
