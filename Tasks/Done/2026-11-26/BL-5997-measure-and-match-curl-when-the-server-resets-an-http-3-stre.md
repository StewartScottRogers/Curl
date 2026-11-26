---
id: BL-5997
title: Measure and match curl when the server resets an HTTP/3 stream
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5996]
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptOutcome.cs, Curl.Original.Protocol.Http.UnitTests/Http3ResetStreamConnectionTests.cs, Record-CurlExchange.ps1, Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/Http3StreamTrace.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpTransferException.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptFailures.cs, Curl.Original.Protocol.Http.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Http.UnitTests/Http3StreamAbortTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5997 — Measure and match curl when the server resets an HTTP/3 stream

## Goal

When the server resets an HTTP/3 request stream, Curl's connection line, connection reuse and
`--trace-config http/3` lines for that transfer and the next URL match curl's.

## Context

ADR-1415 (BL-5996): over HTTP/3 only a transfer that succeeds, times out, or that `-f`,
`--max-filesize` or a failed write ends marks its stream done and keeps its connection
(`HttpProtocolHandler.FailsOnlyItsStream`). A stream the server resets (exit 95, or 18 after
body bytes) was not measured: `Record-CurlExchange.ps1 -Http3StalledOrigin` cannot reset a
stream, so it needs an option that sends RESET_STREAM after the answer. Measure curl 8.18.0's
ngtcp2 build with two URLs to the origin.

## Acceptance criteria

- [x] The Notes record what curl writes and sends when the origin resets the first of two URLs' streams, before and after body bytes.
- [x] Curl's connection line, connection reuse and `--trace-config http/3` stream-done lines match that measurement, pinned by unit tests.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Measured with curl 8.18.0's ngtcp2 build (WinGet) and the recorder's new `-Http3ResetCode` /
`-Http3ResetBeforeAnswer`:
`-Http3StalledOrigin -Http3EndStream -Http3ResetCode 0x102`, curl `--http3-only -k -sS -v
--trace-config http/3 -m 5 <url>/x <url>/y`.

- Reset before any answer and after the head with no body: exit 95. After 5 body bytes: exit 18.
- All three write, in order: `[HTTP/3] [0] reset -> 0`, `[0] RESET: error 258`,
  `[0] quic close(app_error=258) -> 0`, `HTTP/3 stream 0 reset by server`, `[0] easy handle is done`,
  `no active streams, unset keep-alive`, `query conn[0]: MAX_CONCURRENT -> 99 (0 in use)`,
  `Connection #0 to host ... left intact`, then `curl: (95|18) HTTP/3 stream 0 reset by server`.
- `/y`: `Reusing existing https: connection with host 127.0.0.1`, `peer idle timeout`, stream 4,
  succeeds (curl exits 0), `MAX_CONCURRENT -> 98`. No `app_error=268` line for stream 0.
- Sent: nothing the origin can see for the reset stream (its request had ended with FIN); the
  connection stays open and carries stream 4.

Curl, run against the same origin, writes the same lines, except that its failure names the code
(`(error 0x102 INTERNAL_ERROR)`), curl 8.21.0's text kept by ADR-0187. Decisions in ADR-1416.
The upload case (request not yet ended: Curl cancels it with `0x10c`) was not measured. The
`-v` failure line was missing before this task and is added. `Http3StreamAbortTests`' `-f`
case now expects nothing sent for a stream whose reset had already arrived. Files added to
`touches`: no other task in Doing names them. `AttemptOutcome.cs` needed no change.
Quality run of the HTTP library: 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: cfb57401 11f8926c 3d836d5e.
- 2026-11-25: Doing -> Done. Curl keeps and reuses the HTTP/3 connection of a stream the server resets, writing curl 8.18.0's reset and stream-done lines (ADR-1416).
