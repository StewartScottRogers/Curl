---
id: BL-817
title: Pool HTTP/2 connections with their session and match curl's stream window and closing GOAWAY
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-658]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Http2.UnitLibrary, Curl.Http2.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions/ADR-0159-an-http-2-stream-is-read-and-written-as-the-http-1-exchange-curls-own-h2-layer-presents.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-817 — Pool HTTP/2 connections with their session and match curl's stream window and closing GOAWAY

## Goal

An HTTP/2 connection behaves on the wire as curl's does (curl.se nghttp2 build 8.18.0, measured with `Record-CurlExchange.ps1` and `--http2-prior-knowledge` in BL-658): it is pooled with its HTTP/2 session and reused for the next sequential transfer to the same origin, each request's HEADERS is followed by a stream WINDOW_UPDATE growing the receive window to 10 MiB, and closing the connection sends GOAWAY (last stream 0, NO_ERROR, debug data `shutdown`).

## Context

Three differences from curl that BL-658 measured and left open (ADR-0159, "Known differences from curl", and its point 5):

1. **Pooling.** curl pools an HTTP/2 connection and reuses it for the next transfer to the same origin. Curl never marks an HTTP/2 connection reusable (`HttpProtocolHandler.ExchangeOnConnectionAsync`, `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`, `http2 is null` guard) because the pool (`Curl.Networking.UnitLibrary/PoolingConnector.cs`, `PooledConnection.cs`, `PoolEntry.cs`) hands on the bare `IConnection` without its `Http2Session` (`Curl.Protocol.Http.UnitLibrary/Http2Session.cs`: HPACK encoder and decoder tables, next stream ID, peer settings). The pooled connection must carry its session so the next transfer continues it (next odd stream ID, same HPACK state, no second preface), and must not be reused once the peer sent GOAWAY or closed it (`Http2Session.AcceptsNewStreams`). Protocol libraries never reference `Curl.Networking`, so the session travels through something in `Curl.Protocol.Abstractions.UnitLibrary` or stays keyed inside the HTTP library; choose the simplest design, and record it by updating ADR-0159 point 5.
2. **Stream receive window.** Right after a request's HEADERS curl sends WINDOW_UPDATE on that stream with increment 10420225 (65535 + 10420225 = 10 MiB): measured bytes `00 00 04 08 00 00 00 00 01 00 9F 00 01`, sent twice by 8.18.0. Re-measure first and match exactly what curl sends (count and order relative to HEADERS and DATA). Curl sends none; `Curl.Http2.UnitLibrary/Http2Connection.cs` already has `IncreaseStreamReceiveWindowAsync`. Send it from `Http2StreamConnection.cs` / `Http2Session.cs`.
3. **Closing GOAWAY.** When curl closes an HTTP/2 connection it sends GOAWAY with last stream 0, NO_ERROR and debug data `shutdown`: measured bytes `00 00 11 07 00 00 00 00 00 00 00 00 00 00 00 00 00 73 68 75 74 64 6F 77 6E`. Curl sends nothing. `Http2Connection.SendGoAwayAsync` currently sends empty debug data, so it needs a way to send `shutdown`. The GOAWAY goes out whenever the connection is closed: at the end of a transfer that does not pool it, and when the pool disposes an idle HTTP/2 connection (end of the run, or evicted).

Out of scope: `-Z` multiplexing of concurrent transfers over one connection (BL-717) and HTTP/2 `-v`/`-i`/`%{http_version}` output (BL-660). Only the wire bytes and the reuse are in scope; the `-v` "Re-using existing connection" line for HTTP/2 reuse follows whatever the pool already reports.

## Acceptance criteria

- [x] Re-measured with `Record-CurlExchange.ps1 --http2-prior-knowledge` (two sequential URLs on one origin) against curl 8.18.0 or newer nghttp2 build; the request bytes for WINDOW_UPDATE and GOAWAY copied into Notes with the curl version.
- [x] Tests through fake connections in `Curl.Protocol.Http.UnitTests` (and `Curl.Networking.UnitTests` if the pool changes) show two sequential HTTP/2 transfers to one origin using one connection: the second uses stream 3, sends no second preface, decodes with the same HPACK state, and the connection is reported reused; a connection whose peer sent GOAWAY is not reused.
- [x] A test pins the WINDOW_UPDATE bytes after HEADERS exactly as re-measured (count and position).
- [x] A test pins the GOAWAY bytes `00 00 11 07 00 00 00 00 00 00 00 00 00 00 00 00 00 73 68 75 74 64 6F 77 6E` written when an HTTP/2 connection is closed, both for a connection closed after its transfer and for an idle pooled one disposed by the pool.
- [x] ADR-0159 point 5 and "Known differences from curl" state the new pooling, window and GOAWAY behaviour.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

**Measured** with curl.se's nghttp2 build (`curl 8.18.0 ... nghttp2/1.68.0`,
`%LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe`) through `Record-CurlExchange.ps1 -Curl <it>
--http2-prior-knowledge -s`, the server sending empty SETTINGS, the SETTINGS ACK and
`HEADERS :status 200` with END_STREAM, `-ResponseDelayMilliseconds 300 -HoldOpenMilliseconds 1500`:
- Two URLs (`/a`, `/b` on 127.0.0.1:18817), one connection: preface, SETTINGS, connection
  WINDOW_UPDATE; `HEADERS` stream 1; `00 00 04 08 00 00 00 00 01 00 9F 00 01` twice; SETTINGS
  ACK; `HEADERS` stream 3 (`82 86 C0 04 02 2F 62 BF BE`, no second preface, HPACK indexes from
  stream 1); `00 00 04 08 00 00 00 00 03 00 9F 00 00` twice - the increment is 10420224 once
  the client's SETTINGS are acknowledged (65536 initial), 10420225 before (65535).
- One URL, clean end: after the SETTINGS ACK, GOAWAY `00 00 11 07 00 00 00 00 00 00 00 00 00
  00 00 00 00 73 68 75 74 64 6F 77 6E 00` - the debug data is `shutdown` **and a NUL** (length
  0x11 = 8 + 9); the Goal's quoted bytes left the trailing `00` out.
- `-d name=value`: HEADERS, DATA (END_STREAM), then the two WINDOW_UPDATEs; `-T` of 40000
  bytes: HEADERS, three DATA frames, then the two WINDOW_UPDATEs. So the WINDOW_UPDATEs follow
  the frame that ends the request, not HEADERS.

**Design** (ADR-0159 points 5 and 6, updated): the session travels through Abstractions -
`IConnectionSession.ShutDownAsync` plus default members `IConnection.Session` and
`IConnection.TryHoldSession`. `PooledConnection` stores the session on its `PoolEntry`;
`PoolEntry.CloseAsync` shuts the session down before disposing, wherever the pool closes a
connection (not reusable, evicted, expired, dead, pool disposed). A connection that holds no
session gets the GOAWAY from the handler at the end of the exchange. `Http2Connection` gained
`SendGoAwayAsync(errorCode, debugData, ...)` and `IsGoAwaySent`.

**Choices with a sensible default (not measured):** a body stalled on flow control sends the
WINDOW_UPDATEs before its first read; an h2c-upgraded stream sends none (left as BL-716 pinned
it); the session sends no GOAWAY after the peer closed, after one already went out, or before
any preface, and ignores an `IOException` writing it. `ExchangeOnConnectionAsync` hit
complexity 18 and was split (`UnheldHttp2Session`, `ShutDownAsync`, `SettleConnection`).

**Gates.** `dotnet build Curl.slnx -warnaserror` clean, 0 warnings; fast tests pass in all 33
test assemblies; `Measure-CodeQuality.ps1`: Curl.Protocol.Http, Curl.Http2, Curl.Networking and
Curl.Protocol.Abstractions each 100% line and branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. HTTP/2 connections are pooled with their session and send curl's stream WINDOW_UPDATEs and closing GOAWAY
