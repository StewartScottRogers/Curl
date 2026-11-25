---
id: BL-5959
title: Shut the HTTP/2 connection down for -I -X GET when a trailer or DATA ends the stream short of content-length
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Http2IgnoredBodyLengthTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5959 — Shut the HTTP/2 connection down for -I -X GET when a trailer or DATA ends the stream short of content-length

## Goal

`curl -I -X GET` over HTTP/2 shuts the connection down as curl does when a trailer or a DATA frame ends the stream short of `content-length`, still exiting 0.

## Context

Measured in BL-5947 with `Record-CurlExchange.ps1 -ListenAddress <WSL gateway> -Curl wsl.exe` (curl 8.18.0, nghttp2), `--http2-prior-knowledge -sS -v -I -X GET`. All three exit 0, send GOAWAY PROTOCOL_ERROR, and write only the status line, headers and empty line to stdout:

- `-Http2Frames 'H :status: 200|content-length: 3','H! x-t: v'`: after `< `, `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`, `{ [0 bytes data]`, `* shutting down connection #0`; the trailer is neither shown nor written.
- `'H :status: 200|content-length: 3','D! a'`: `{ [1 bytes data]`, then the same three lines.
- `'H :status: 200|content-length: 3','D! '`: the same three lines as the trailer case.

`Http2StreamConnection.EndResponseAsync` skips the check when `EndsAtFinalHead` (the trailer), and `EndAtUnwantedBodyAsync` only shuts down for DATA past the length.

## Acceptance criteria

- [x] A unit test pins exit 0, the GOAWAY PROTOCOL_ERROR, the `-v` lines in order and the output for each of the three frame sequences.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The pinned lines and output are the BL-5947 measurements under Context; curl was not measured again. No ADR: this matches curl and chooses nothing.
- A trailer is short when the head's `content-length` exceeds the DATA received: it is dropped, and the handler's existing empty-data report gives the `{ [0 bytes data]` after the shutdown line. DATA is short only when it also ends the stream.
- Measure-CodeQuality.ps1 for `Curl.Original.Protocol.Http.UnitLibrary`: 100% line and branch, 0 failing members (after splitting out `ShutDownAtUnwantedDataAsync` for complexity).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 3f303627 3d836d5e.
- 2026-11-25: Doing -> Done. -I -X GET over HTTP/2 shuts the connection down with GOAWAY PROTOCOL_ERROR and exits 0 when a trailer or DATA ends the stream short of content-length
