---
id: BL-5996
title: Mark an HTTP/3 request stream done when its transfer fails
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5995]
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitTests/Http3FailedTransferStreamTests.cs, Record-CurlExchange.ps1, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptOutcome.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5996 — Mark an HTTP/3 request stream done when its transfer fails

## Goal

After an HTTP/3 transfer that fails without timing out, Curl treats its stream as finished the way curl does, so the next URL on the connection gets curl's `--trace-config http/3` lines.

## Context

ADR-1413 (BL-5995), ADR-0388 consequences. Only a successful transfer
(`Http3StreamConnection.ReportTransferDone`) and a timed-out one (`HttpProtocolHandler.EndTimedOutStreamAsync`)
mark a stream done with `Http3Session.EndRequestStream`. After any other failure (`-f` on a
status of 400 or above, for one) the session counts the stream as active: the next stream writes no
`peer idle timeout` line and `query conn[<n>]` over-counts the streams in use. Measure curl first;
`Record-CurlExchange.ps1 -Http3StalledOrigin` answers 200 only, so it needs a status option for `-f`.

## Acceptance criteria

- [x] The Notes record the lines curl writes when a `-f` HTTP/3 transfer fails and during the next URL on the connection.
- [x] Curl writes those lines there, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 (ngtcp2, WinGet), `Record-CurlExchange.ps1 -Http3StalledOrigin`,
  `--http3-only -k -sS -v --trace-config http/3 -m 5 -f <url>/x <url>/y`, both answered 500, exit 22.
  When `/x` fails: `The requested URL returned error: 500`, `[0] error 22 writing 2 bytes of headers`,
  `[0] xfer write failed`, `[0] easy handle is done`, `no active streams, unset keep-alive`,
  `query conn[0]: MAX_CONCURRENT -> 99 (0 in use)`, `Connection #0 to host ... left intact`.
  During `/y`: `Reusing existing https: connection with host 127.0.0.1`,
  `peer idle timeout is 300000ms, set keep-alive to 150000 ms.`, `[4] OPENED stream`,
  `[0] quic close(app_error=268) -> 0`, then the same ending for stream 4 with `-> 98 (0 in use)`.
- `-f` already did this since BL-5983 (ADR-1414); this task's tests pin the `peer idle timeout` line on the next URL.
- The other failures measured the same way, `--max-filesize 1` (exit 63) and `-o` into a missing
  directory (exit 23): same ending and reuse, STOP_SENDING 0x10c on a stream left open, nothing sent
  and `quic close(app_error=256)` on one ended (`-Http3EndStream`). Curl closed the connection and
  never marked the stream done; it now matches (ADR-1415). Curl.Console run against the three cases
  wrote curl's connection and stream-done lines and wire bytes. The `error <n> writing`,
  `xfer write failed` and `read_stream` lines stay unwritten (ADR-1414).
- HTTP/2 left as it was for 63 and 23 (not measured). Server stream resets: BL-5997.
- The recorder already answered a chosen status (BL-5983), so `Record-CurlExchange.ps1` is unchanged;
  `Http3StreamConnection.cs` needed no change.
- Added to touches: `HttpProtocolHandler.AttemptOutcome.cs`, `HttpProtocolHandler.ConnectionEnd.cs`
  and the library's `CLAUDE.md`; no other task in Doing names them.
- Quality: Curl.Original.Protocol.Http.UnitLibrary 0 failing members. Build clean, fast tests green.
  `dotnet format --verify-no-changes` reports import order in `Http2RequestHeaders.cs`, a file this
  task did not change.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 9e1bad8b fe0e6ee6 3d836d5e.
- 2026-11-25: Doing -> Done. An HTTP/3 transfer -f, --max-filesize or a failed write ends marks its stream done and keeps its connection, so the next URL gets curl's --trace-config http/3 lines (ADR-1415)
