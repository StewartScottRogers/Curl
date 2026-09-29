---
id: BL-716
title: Upgrade a cleartext HTTP/1.1 request to h2c for --http2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-659]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-716 — Upgrade a cleartext HTTP/1.1 request to h2c for --http2

## Goal

`curl --http2 http://host/` sends curl 8.21.0's HTTP/1.1 upgrade request (`Connection: Upgrade, HTTP2-Settings`, `Upgrade: h2c`, `HTTP2-Settings: <base64url SETTINGS>`), switches to HTTP/2 on `101 Switching Protocols` (the response arriving on stream 1) and carries on over HTTP/1.1 when the server ignores the upgrade, on every platform.

## Context

- Conformance audit 2026-09-28, row 32; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-659 (HTTP/2 path, option acceptance) and BL-658 (request and response on a stream). RFC 7540 section 3.2 (the h2c upgrade; RFC 9113 deprecates it but curl still sends it for `--http2` over `http://`: confirm by measurement).
- Measure with an OpenSSL build of curl through `Record-CurlExchange.ps1`: `--http2 -v http://127.0.0.1:<P>/` against the plain HTTP/1.1 server (the upgrade headers curl sends, and what it does when ignored), and against an h2c-capable server through `-NoServer` if one is available (for example `nghttpd --no-tls`).

## Acceptance criteria

- [x] Measured first as above; request bytes and stderr copied into Notes.
- [x] `Curl.Protocol.Http.UnitTests` pin the upgrade request bytes, the switch to HTTP/2 after a `101` with the response read from stream 1, and the unchanged HTTP/1.1 path when the server answers `200` without upgrading, through a fake connection.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measurement (2026-09-29)

Which curl: the only builds here with HTTP/2 are curl.se's nghttp2 build of curl 8.18.0
(`%LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe`, LibreSSL, nghttp2 1.68.0), the build
BL-658 and ADR-0141 already measured HTTP/2 with, and WSL Ubuntu's OpenSSL curl 8.18.0.
WSL's curl cannot reach the recorder's 127.0.0.1 listener (exit 7, NAT networking), and a
cleartext upgrade never touches the TLS backend, so the curl.se build was used. The Git for
Windows reference curl 8.21.0 is Schannel without HTTP2 and ignores `--http2` over cleartext.

`Record-CurlExchange.ps1 -Curl <curl.se curl> -Port 48716 -CurlArgs '--http2','-v','http://127.0.0.1:48716/'`,
server answering `HTTP/1.1 200 OK`, `Content-Length: 3`, `hi\n` (upgrade ignored):

```
GET / HTTP/1.1
Host: 127.0.0.1:48716
User-Agent: curl/8.18.0
Accept: */*
Upgrade: h2c
HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA
Connection: Upgrade, HTTP2-Settings

```

stderr (progress meter dropped): `* using HTTP/1.x`, the request lines as above,
`* Request completely sent off`, `< HTTP/1.1 200 OK`, `< Content-Length: 3`, `< `,
`{ [3 bytes data]`, `* Connection #0 to host 127.0.0.1:48716 left intact`. Exit 0, stdout `hi\n`.

`HTTP2-Settings` is the base64url of the preface's SETTINGS payload: MAX_CONCURRENT_STREAMS 100,
INITIAL_WINDOW_SIZE 65536, ENABLE_PUSH 0.

The same with `-HoldOpenMilliseconds 1500` and a canned `101` followed by HTTP/2 frames:
`HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n`, then an empty
SETTINGS, HEADERS on stream 1 (`88`, `:status 200`, END_HEADERS), DATA on stream 1 `hi\n` with
END_STREAM. curl sent, after the request head: the preface, SETTINGS (same payload),
WINDOW_UPDATE 0 +1048510465, SETTINGS ACK, and at exit GOAWAY NO_ERROR (`shutdown`). No
HEADERS: stream 1 is the upgraded request. stderr:

```
* using HTTP/1.x
> GET / HTTP/1.1 ... (as above)
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
* Connection #0 to host 127.0.0.1:48717 left intact
```

With `-sSi` stdout is `HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\nHTTP/2 200 \r\n\r\nhi\n`.

Where the headers go (all measured the same way):
- `Upgrade: h2c` and `HTTP2-Settings` follow `Accept`, `TE`, `Referer` and
  `Proxy-Connection`, and come before `Cookie`, `If-Modified-Since` and the `-H` values.
- `Connection` goes last, after `Content-Length`/`Content-Type`. Its value is the `-H
  Connection` value, then `TE` (`--tr-encoding`), then `Upgrade, HTTP2-Settings`: `-H
  'Connection: close'` gives `Connection: close, Upgrade, HTTP2-Settings`, and
  `--tr-encoding` gives `Connection: TE, Upgrade, HTTP2-Settings`. `-H 'Connection:'` gives
  `Connection: Upgrade, HTTP2-Settings`.
- `-H 'Upgrade: foo'` is sent after curl's own `Upgrade: h2c`.
- GET, HEAD (`-I`), POST (`-d`), PUT (`-X PUT`) and a forward proxy (`-x`) all upgrade.
- `-0` after `--http2` sends a plain HTTP/1.0 request.

### Decisions (Claude, under Stewart's delegation)

- `--http2` needed a value the handler can see: `HttpVersionPreference.Http2`, appended to the
  enum. `Curl.Protocol.Abstractions.UnitLibrary` and its tests were added to `touches`; no
  task in Doing named them. The Console mapping from `RequestedHttpVersion.Http2` is filed as
  BL-865, because `Curl.Console` belongs to BL-602 in Doing.
- These choices follow the standing rule of matching measured curl, so they are recorded here
  rather than in an ADR. `Documentation/Planning/Decisions` belongs to BL-785 in Doing.
- The upgrade is asked for on every HTTP/1.x exchange under `Http2` whose connection is not
  TLS. Over TLS the ALPN offer decides, as before.
- `HttpH2cUpgradeConnection` watches only the first head. Anything other than
  `HTTP/1.1 101` (a `100 Continue` first, `HTTP/1.0 101`, a close) passes through unchanged
  as HTTP/1.1.
- The frame layer needed no change: `Http2Connection.OpenStream` without HEADERS gives
  stream 1, and its response is read as any other. That stream is never ended locally, so it
  counts against the peer's concurrency limit for the connection's life. That is harmless
  while the connection is not reused.
- Follow-up BL-866: after an upgrade the connection is not reused, and `-v` ends with the
  shutting-down line where curl prints `left intact`. Retries and same-origin redirects then
  go out on a new connection.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --http2 over cleartext sends curl's h2c upgrade request, switches to HTTP/2 stream 1 on 101 and stays on HTTP/1.1 when the server ignores it (handler level; Console mapping is BL-865)
