---
id: BL-658
title: Send an HTTP/2 request and read its response on a stream
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-656, BL-657, BL-668]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Http2.UnitLibrary, Curl.Http2.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-658 — Send an HTTP/2 request and read its response on a stream

## Goal

When a connection speaks HTTP/2, the HTTP handler sends the request as HEADERS (pseudo-headers and header order as curl 8.21.0 sends them, lower-cased names) and DATA frames, reads the response's HEADERS, DATA and trailers, and feeds the same output, `-i`/`-D`, `-f`, redirect, auth, cookie and progress paths as HTTP/1.1, with stream errors mapped to curl's exits (92 `CURLE_HTTP2_STREAM`, 16 `CURLE_HTTP2`).

## Context

- Conformance audit 2026-09-28, row 32. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): HTTP/2 is offered on every platform. Builds on BL-656 (HPACK) and BL-657 (frames) in `Curl.Http2.UnitLibrary`: add that `ProjectReference` to `Curl.Protocol.Http.UnitLibrary` (allowed by BL-668) and amend `Curl.Protocol.Http.UnitLibrary/CLAUDE.md` to name it. Keep the request/response plumbing version-neutral so the HTTP/3 path (BL-731) reuses it.
- The handler's HTTP/1.1 path (`Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` and its request writer and head reader) is the model; the version choice itself (ALPN, prior knowledge) is BL-659.

## Acceptance criteria

- [x] `Curl.Protocol.Http.UnitTests` pin the HEADERS block for a plain GET, a POST with `-d`, and custom `-H` headers, and the output for a response with a body, with trailers, and with RST_STREAM mid-body, through a fake connection.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

**Design (ADR-0159, decided by Claude under Stewart's delegation).** The HTTP/2 stream is
presented to the existing HTTP/1.1 exchange as the bytes libcurl's own h2 layer hands its
HTTP/1 parser, so every output, `-f`, redirect, auth, cookie and progress path is shared
rather than rebuilt. `Http2Session` (per connection: `Http2Connection` over
`HttpConnectionStream`, the HPACK encoder and decoder, preface on first use) and
`Http2StreamConnection` (one stream as an `IConnection`: first write is the HTTP/1 head sent
as HEADERS via `Http2RequestHeaders`, later writes are DATA within flow control; reads give
`Http2ResponseHead`'s `HTTP/2 200 \r\n` heads, then DATA, then 0; trailers kept apart).
The plumbing is version-neutral in the sense BL-731 needs: an HTTP/3 stream can be another
`IConnection` of the same shape.

**Trigger (Abstractions, added to touches).** The handler speaks HTTP/2 with the new
`HttpVersionPreference.Http2PriorKnowledge`, or when the connect result's new
`ConnectResult.ApplicationProtocol` is `h2`. BL-659 wires both (CLI option, ALPN). No task in
Doing touched Abstractions (BL-559: IMAP/Output/Console tests; BL-722: QUIC).

**Http2 library (added to touches).** `Http2Connection.ReadStreamFrameAsync` waits for a
stream frame, so a body waiting for window could never see a WINDOW_UPDATE the server sends
alone: added `ReadFrameAsync` (one frame) and `IsClosedByPeer`, with tests.

**Measured** with curl.se's nghttp2 build 8.18.0 (`%LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe`;
the Windows reference has no HTTP/2) through `Record-CurlExchange.ps1 -Curl <it>
--http2-prior-knowledge`, serving hand-built frames as `-Response` with
`-ResponseDelayMilliseconds 300 -HoldOpenMilliseconds 1500` (the script's CRLFCRLF read stops
inside the preface, which is fine: the delay lets HEADERS arrive before the reply):
- GET `/a?b=1`: HEADERS flags 0x05, block `82 86 41 8b…(authority) 04 85…(path, unindexed)
  7a 88…(user-agent) 53 03 2a2f2a`. POST `-d name=value`: flags 0x04, then
  `0f 0d 02 "10"` (content-length) and `5f 98…` (content-type), and one DATA frame carrying
  END_STREAM. Custom `-H`: `:authority` from `-H Host`, `connection` dropped, `te: trailers`
  only, order UA, x-custom, accept, te. All three pinned byte for byte (with `-A curl/8.18.0`).
- `-T` of 2 MB over HTTP/2: no `Expect`, `content-length` sent, DATA in 16384-byte frames.
- Output: `HTTP/2 200 \r\n` + headers as received + `\r\n` + body; trailers
  `x-checksum: abc\r\nx-second: two\r\n` after the body, no empty line.
- RST_STREAM mid-body: exit 92 `HTTP/2 stream 1 was not closed cleanly: INTERNAL_ERROR (err 2)`
  (`CANCEL (err 8)` for 8). Unexpected CONTINUATION: exit 16 `nghttp2 shuts down connection
  with error 1: PROTOCOL_ERROR`. GOAWAY INTERNAL_ERROR mid-body: exit 56 `Failure when
  receiving data from the peer`. Clean close mid-body: exit 18 `Transferred a partial file`;
  before the head: exit 16 `Error in the HTTP2 framing layer`. (A first attempt at the close
  gave `Recv failure: Connection was aborted`: the script closed with curl's SETTINGS ACK
  unread, which Windows turns into a reset; `-HoldOpenMilliseconds 400` gives a clean FIN.)

**Choices with a sensible default (not measured):** an empty `-d ''` body sets END_STREAM on
HEADERS (bodyLength 0); an undecodable header block is exit 16 with COMPRESSION_ERROR's name;
a head with no valid `:status` or DATA before the head is reset with PROTOCOL_ERROR and
fails exit 92, as nghttp2 treats a malformed response; `-v`'s `using HTTP/2` replaces
`using HTTP/1.x` and the reported request line says `HTTP/2` (ADR-0141's measurement); the
rest of `-v` is BL-660.

**Side fixes.** `HttpRequestHeadFormatter` wrote `Content-Length: 0` for an unchunked body of
unknown length, a combination only HTTP/2 framing produces; it now writes none. The quality
run failed four members on complexity - three this change grew (`ExchangeOnConnectionAsync`,
`ExchangeAsync`, `AppendBodyHeaders`) and one it did not (`HttpTransferDeadline..ctor`, 12);
all four were split so the library reports no failing member. The HTTP/2 stream's end is
sent from `HttpRequestBodyWriter.WriteAsync`, where the body ends.

**Known differences, filed as BL-812:** curl pools HTTP/2 connections (Curl never marks one
reusable: the pool would lose its session), grows each stream window to 10 MiB after
HEADERS, and sends GOAWAY `shutdown` on close.

**Gates.** `dotnet build Curl.slnx -warnaserror` clean; fast tests all pass (Http 1210,
Http2 218, Abstractions 591); `Measure-CodeQuality.ps1` 100% line and branch, 0 failing
members for `Curl.Protocol.Http.UnitLibrary`, `Curl.Http2.UnitLibrary` and
`Curl.Protocol.Abstractions.UnitLibrary`. `dotnet format --verify-no-changes` reports nothing
in any file this task changed (it does flag line endings in
`Curl.Console.UnitTests/DumpHeaderOutputStreamTests.cs`, which this task does not touch).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The HTTP handler sends a request as HEADERS and DATA on an HTTP/2 stream and reads its heads, body and trailers through the HTTP/1.1 paths, with curl's exits 92, 16, 56 and 18
