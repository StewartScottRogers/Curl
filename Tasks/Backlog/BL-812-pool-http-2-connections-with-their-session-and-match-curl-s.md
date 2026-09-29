---
id: BL-812
title: Pool HTTP/2 connections with their session and match curl's stream window and closing GOAWAY
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-658]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Http2.UnitLibrary, Curl.Http2.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions/ADR-0159-an-http-2-stream-is-read-and-written-as-the-http-1-exchange-curls-own-h2-layer-presents.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-812 — Pool HTTP/2 connections with their session and match curl's stream window and closing GOAWAY

## Goal

An HTTP/2 connection behaves on the wire as curl's does (curl.se nghttp2 build 8.18.0, measured with `Record-CurlExchange.ps1` and `--http2-prior-knowledge` in BL-658): it is pooled with its HTTP/2 session and reused for the next sequential transfer to the same origin, each request's HEADERS is followed by a stream WINDOW_UPDATE growing the receive window to 10 MiB, and closing the connection sends GOAWAY (last stream 0, NO_ERROR, debug data `shutdown`).

## Context

Three differences from curl that BL-658 measured and left open (ADR-0159, "Known differences from curl", and its point 5):

1. **Pooling.** curl pools an HTTP/2 connection and reuses it for the next transfer to the same origin. Curl never marks an HTTP/2 connection reusable (`HttpProtocolHandler.ExchangeOnConnectionAsync`, `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`, `http2 is null` guard) because the pool (`Curl.Networking.UnitLibrary/PoolingConnector.cs`, `PooledConnection.cs`, `PoolEntry.cs`) hands on the bare `IConnection` without its `Http2Session` (`Curl.Protocol.Http.UnitLibrary/Http2Session.cs`: HPACK encoder and decoder tables, next stream ID, peer settings). The pooled connection must carry its session so the next transfer continues it (next odd stream ID, same HPACK state, no second preface), and must not be reused once the peer sent GOAWAY or closed it (`Http2Session.AcceptsNewStreams`). Protocol libraries never reference `Curl.Networking`, so the session travels through something in `Curl.Protocol.Abstractions.UnitLibrary` or stays keyed inside the HTTP library; choose the simplest design, and record it by updating ADR-0159 point 5.
2. **Stream receive window.** Right after a request's HEADERS curl sends WINDOW_UPDATE on that stream with increment 10420225 (65535 + 10420225 = 10 MiB): measured bytes `00 00 04 08 00 00 00 00 01 00 9F 00 01`, sent twice by 8.18.0. Re-measure first and match exactly what curl sends (count and order relative to HEADERS and DATA). Curl sends none; `Curl.Http2.UnitLibrary/Http2Connection.cs` already has `IncreaseStreamReceiveWindowAsync`. Send it from `Http2StreamConnection.cs` / `Http2Session.cs`.
3. **Closing GOAWAY.** When curl closes an HTTP/2 connection it sends GOAWAY with last stream 0, NO_ERROR and debug data `shutdown`: measured bytes `00 00 11 07 00 00 00 00 00 00 00 00 00 00 00 00 00 73 68 75 74 64 6F 77 6E`. Curl sends nothing. `Http2Connection.SendGoAwayAsync` currently sends empty debug data, so it needs a way to send `shutdown`. The GOAWAY goes out whenever the connection is closed: at the end of a transfer that does not pool it, and when the pool disposes an idle HTTP/2 connection (end of the run, or evicted).

Out of scope: `-Z` multiplexing of concurrent transfers over one connection (BL-717) and HTTP/2 `-v`/`-i`/`%{http_version}` output (BL-660). Only the wire bytes and the reuse are in scope; the `-v` "Re-using existing connection" line for HTTP/2 reuse follows whatever the pool already reports.

## Acceptance criteria

- [ ] Re-measured with `Record-CurlExchange.ps1 --http2-prior-knowledge` (two sequential URLs on one origin) against curl 8.18.0 or newer nghttp2 build; the request bytes for WINDOW_UPDATE and GOAWAY copied into Notes with the curl version.
- [ ] Tests through fake connections in `Curl.Protocol.Http.UnitTests` (and `Curl.Networking.UnitTests` if the pool changes) show two sequential HTTP/2 transfers to one origin using one connection: the second uses stream 3, sends no second preface, decodes with the same HPACK state, and the connection is reported reused; a connection whose peer sent GOAWAY is not reused.
- [ ] A test pins the WINDOW_UPDATE bytes after HEADERS exactly as re-measured (count and position).
- [ ] A test pins the GOAWAY bytes `00 00 11 07 00 00 00 00 00 00 00 00 00 00 00 00 00 73 68 75 74 64 6F 77 6E` written when an HTTP/2 connection is closed, both for a connection closed after its transfer and for an idle pooled one disposed by the pool.
- [ ] ADR-0159 point 5 and "Known differences from curl" state the new pooling, window and GOAWAY behaviour.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
