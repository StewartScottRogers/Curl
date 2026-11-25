---
id: BL-5958
title: Write curl's nghttp2 shuts down -v line when HTTP/2 DATA or a header field makes a response malformed
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Cgr138Tests.cs, Documentation/Planning/Decisions/ADR-0847-http2-responses-whose-length-does-not-add-up-shut-the-connection-down.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5958 — Write curl's nghttp2 shuts down -v line when HTTP/2 DATA or a header field makes a response malformed

## Goal

`-v` shows curl's nghttp2 line before `* closing connection #0` for every HTTP/2 response Curl shuts the connection down for, not only for a head that ends the stream.

## Context

BL-5947 measured `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR` after the head lines (curl 8.18.0) and added it for a refused head only, through `HttpTransferException.InfoLines`. The other `ShutDownMalformedAsync` calls in `Http2StreamConnection` (DATA past or short of `content-length`, headers after the head without END_STREAM, an invalid header field, whose message differs) set no `InfoLines`. Measure each with `Record-CurlExchange.ps1 -Http2Frames` before pinning the lines and their order; a failure thrown from a body read may also need `HttpProtocolHandler.TransferFailures.cs` (add it to `touches`).

## Acceptance criteria

- [x] A unit test pins the measured `-v` lines for DATA past and short of `content-length` and for an invalid header field.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-11-25, curl 8.18.0 (WSL, nghttp2 1.68.0), `--http2-prior-knowledge -sS -v`, `-Http2Frames`: DATA past `content-length` (first or second frame), DATA or a trailer ending short of it, headers after the head without END_STREAM, DATA on a 204: exit 16, `-v` writes `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR` before `* closing connection #0`, after the `{ [N bytes data]` line of the DATA before the failure.
- An invalid header field (upper-case name in a head, in a trailer, after no `:status` (exit 8); `content-length: abc`): `* Invalid HTTP header field was received: ...` then the shutdown line, and the field message is the `curl: (N)` error.
- So every `ShutDownMalformedAsync` path now sets `InfoLines`; `HttpProtocolHandler.TransferFailures.cs` was not needed (body-read failures already report them). No new ADR: same decision as ADR-0847 (match curl), amended; added to `touches`, no task in Doing names it.
- `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members. Build clean, fast tests green.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 1c4d76b9 3d836d5e.
- 2026-11-25: Doing -> Done. Every malformed HTTP/2 shutdown path writes curl's measured -v lines.
