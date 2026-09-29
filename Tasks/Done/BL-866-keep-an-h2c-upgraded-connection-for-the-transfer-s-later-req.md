---
id: BL-866
title: Keep an h2c-upgraded connection for the transfer's later requests and report it left intact
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-716]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Http2.UnitLibrary, Curl.Http2.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-866 — Keep an h2c-upgraded connection for the transfer's later requests and report it left intact

## Goal

After `--http2` upgrades a cleartext connection to h2c, the transfer's later requests on it (a 401 retry, a followed redirect to the same origin, the next URL) go out as new HTTP/2 streams (3, 5, ...) on that connection, and `-v` ends with `Connection #0 to host H:P left intact` as curl prints it.

## Context

- BL-716 made the upgrade work for one exchange: `HttpH2cUpgradeConnection` switches the exchange to stream 1 after a `101`. Because the head reader sees a `101`, `HttpResponseHeadReader.SwitchedProtocols` makes `HttpProtocolHandler.DeliveredWhole` false. So the connection is never reused, a retry or same-origin redirect goes out on a fresh connection, and `-v` ends with the shutting-down line rather than curl's `left intact`.
- Measured in BL-716's Notes: curl.se's nghttp2 curl 8.18.0 (`%LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe`) prints `* Connection #0 to host 127.0.0.1:48717 left intact` after the upgraded response.
- Measure curl first with `Record-CurlExchange.ps1 -Curl <that curl> -Connections 1 -HoldOpenMilliseconds ...`: two URLs on one command line after an upgrade, and a 401 answered over the upgraded stream with `--anyauth`/`-u`.
- Likely shape: once the upgrade connection switches, hand its `Http2Session` to `ExchangeOnConnectionAsync` as the connection's `IHttpStreamSession`, so later exchanges and `KeepsAlive` take the existing HTTP/2 path.

## Acceptance criteria

- [x] curl measured first as above; request bytes and stderr copied into Notes.
- [x] `Curl.Protocol.Http.UnitTests` pin, through a fake connection, a 401 answered on the upgraded stream 1 being retried as HEADERS on stream 3 of the same connection, and the `left intact` line after an upgraded exchange.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measurement (2026-09-29)

curl.se's nghttp2 curl 8.18.0 (`%LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe`), the build
BL-716 measured with. `Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500`, one connection,
the server answering `HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n`,
an empty SETTINGS, then stream 1's response. The recorder answers once per connection, so
the request on stream 3 goes unanswered and curl ends with exit 16 after it; what matters is
what it sent and printed before.

**Two URLs** (`--http2 -v http://127.0.0.1:48866/a http://127.0.0.1:48866/b`, stream 1:
HEADERS `88` then DATA `hi\n` END_STREAM). Request bytes after the HTTP/1.1 upgrade request
(`GET /a`, `Upgrade: h2c`, `HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA`, `Connection: Upgrade, HTTP2-Settings`):

```
PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n
000012 04 00 00000000 0003 00000064 0004 00010000 0002 00000000   SETTINGS
000004 08 00 00000000 3e7f0001                                    WINDOW_UPDATE 0
000000 04 01 00000000                                             SETTINGS ACK
000006 04 00 00000000 0004 00010000                               SETTINGS INITIAL_WINDOW_SIZE 65536
000022 01 05 00000003 8286 418b089d5c0b8170dc69e79c73 04022f62 7a8825b650c3cb85e5c1 53032a2f2a   HEADERS stream 3, /b
000004 08 00 00000003 009f0001   (twice)                          WINDOW_UPDATE stream 3
```

stderr (progress meter and `Note:` lines dropped):

```
* using HTTP/1.x
> GET /a HTTP/1.1 ... (the upgrade request)
* Request completely sent off
< HTTP/1.1 101 Switching Protocols
< Connection: Upgrade
< Upgrade: h2c
< 
* Received 101, Switching to HTTP/2
* Copied HTTP/2 data in stream buffer to connection buffer after upgrade: len=31
< HTTP/2 200 
< 
{ [3 bytes data]
* Connection #0 to host 127.0.0.1:48866 left intact
* Reusing existing http: connection with host 127.0.0.1
* [HTTP/2] [3] OPENED stream for http://127.0.0.1:48866/b
* [HTTP/2] [3] [:method: GET] ... [accept: */*]
> GET /b HTTP/2
> Host: 127.0.0.1:48866
> User-Agent: curl/8.18.0
> Accept: */*
> 
* Request completely sent off
* closing connection #0
curl: (16) Error in the HTTP2 framing layer
```

**401 retry** (`--http2 -v --anyauth -u a:b http://127.0.0.1:48867/a`, stream 1: HEADERS
`:status 401`, `www-authenticate: Basic realm="x"`, then DATA `no\n` END_STREAM). After the
preface, WINDOW_UPDATE and SETTINGS ACK: `000004 03 00 00000001 00000005` (RST_STREAM stream 1,
STREAM_CLOSED), the same SETTINGS INITIAL_WINDOW_SIZE 65536, HEADERS on stream 3 (flags 05)
carrying `authorization: Basic YTpi`, and the two stream-3 WINDOW_UPDATEs. stderr:

```
* Received 101, Switching to HTTP/2
* Copied HTTP/2 data in stream buffer to connection buffer after upgrade: len=53
< HTTP/2 401 
< www-authenticate: Basic realm="x"
* Ignoring the response-body
< 
* Connection #0 to host 127.0.0.1:48867 left intact
* Issue another request to this URL: 'http://127.0.0.1:48867/a'
* Reusing existing http: connection with host 127.0.0.1
* [HTTP/2] [3] OPENED stream for http://127.0.0.1:48867/a
* [HTTP/2] [3] ... [authorization: Basic YTpi] ...
> GET /a HTTP/2
> Host: 127.0.0.1:48867
> Authorization: Basic YTpi
> User-Agent: curl/8.18.0
> Accept: */*
> 
* Request completely sent off
* closing connection #0
curl: (16) Error in the HTTP2 framing layer
```

### What was built

- `HttpH2cUpgradeConnection` exposes the `Http2Session` it switched to and stream 1
  (`UpgradedSession`, `UpgradedStream`).
- `HttpProtocolHandler`: an exchange that switched to HTTP/2 counts as delivered whole, keeps
  alive while the session accepts new streams, and reads stream 1 to its end for its trailers.
  The outcome carries the upgraded session; `ExchangeWithRetriesAsync` sends later retries
  framed for HTTP/2 on it (stream 3, 5, ...), and `ExchangeOnConnectionAsync` hands it to the
  connection like any HTTP/2 session (BL-817), so the next URL to the origin continues it and
  `-v` ends with `left intact`.
- `Curl.Http2.UnitLibrary` and `Curl.Http2.UnitTests` were added to `touches`: stream 1 was
  opened as a plain open stream, so it never closed and held a slot against the peer's
  MAX_CONCURRENT_STREAMS for the connection's life; a server allowing one stream would fail
  the retry with exit 16. `Http2Connection.OpenUpgradedStream` opens it half closed by the
  client (RFC 7540 section 3.2). No task in Doing named `Curl.Http2.*`.
- These choices follow the standing rule of matching measured curl, so no ADR.

### Left for other tasks

- The `Issue another request` line before an authentication retry on the same connection is
  BL-959 (already filed); the `Reusing existing` and `[HTTP/2] [3] OPENED` lines come from the
  existing pooled-HTTP/2 path for the next URL.
- The extra SETTINGS INITIAL_WINDOW_SIZE 65536 and the RST_STREAM on stream 1 for an ignored
  body are not sent: filed as BL-970.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. After an h2c upgrade the connection keeps its HTTP/2 session: a 401 retry and the next URL go out on stream 3, and -v ends with left intact
