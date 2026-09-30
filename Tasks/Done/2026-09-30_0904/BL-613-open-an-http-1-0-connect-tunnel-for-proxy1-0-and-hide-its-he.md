---
id: BL-613
title: Open an HTTP/1.0 CONNECT tunnel for --proxy1.0 and hide its headers with --suppress-connect-headers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-612]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-613 — Open an HTTP/1.0 CONNECT tunnel for --proxy1.0 and hide its headers with --suppress-connect-headers

## Goal

With `--proxy1.0`, the proxy request (`CONNECT` or forwarded request) is sent as HTTP/1.0 exactly as curl 8.21.0 sends it; with `--suppress-connect-headers`, the `CONNECT` reply head is left out of `-i`/`-D` output as curl leaves it out.

## Context

- Conformance audit 2026-09-28, row 16 (Major). Options: BL-612.
- Code: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs` (request line; ADR-0023), the reply head reporting that feeds `-i` (ADR-0052 pseudo-headers, `Curl.Console/HeaderLineTeeStream.cs`).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Connections 2` as the proxy: `--proxy1.0 127.0.0.1:<P> -p http://h/` and a plain forward request, and `-p -x ... -i` with and without `--suppress-connect-headers`; request bytes and stdout copied into Notes.
- [x] Tests pin the request bytes and `-i` output for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-30, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Port 18613 -HoldOpenMilliseconds 1500`,
the proxy answering on one connection with the CONNECT reply and then the origin's response
(`HTTP/1.1 200 Connection established\r\nX-Proxy: yes\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi`).
One connection was enough: the tunnel carries the request on the proxy connection.

- `-sS -p --proxy1.0 127.0.0.1:18613 http://example.com/` sent
  `CONNECT example.com:80 HTTP/1.0\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n`
  then `GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`; stdout `hi`.
  This was already implemented (ADR-0023, `BuildConnectRequest_ForAnHttp10Proxy_UsesHttp10`).
- `-sS --proxy1.0 127.0.0.1:18613 http://example.com/` (forward) sent
  `GET http://example.com/ HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n`:
  HTTP/1.1, as already pinned by `ExecuteAsync_Http10Proxy_ForwardsLikeAnHttp11Proxy`.
- `-sS -p -x 127.0.0.1:18613 -i http://example.com/` sent the HTTP/1.1 CONNECT and GET; stdout
  `HTTP/1.1 200 Connection established\r\nX-Proxy: yes\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi`.
- The same with `--suppress-connect-headers`: stdout `HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi`.
- `-D -` instead of `-i`: stdout the same as `-i`'s; with `--suppress-connect-headers`, the same as `-i`'s with it.
- `-I`: `HEAD / HTTP/1.1` through the tunnel; stdout the CONNECT head then `HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n`.
- `--proxy1.0 ... -i`, the proxy answering `HTTP/1.0 200 OK\r\n\r\n`: stdout `HTTP/1.0 200 OK\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi`.
- `-i`, the proxy answering `HTTP/1.1 403 Forbidden\r\nContent-Length: 3\r\n\r\nno\n`: stdout
  `HTTP/1.1 403 Forbidden\r\nContent-Length: 3\r\n\r\n`, stderr `curl: (7) CONNECT tunnel failed, response 403`, exit 7.
- `-i -U u:p --proxy-anyauth`, the proxy answering 407 with Basic: the 407's head was written too.

What was missing, and now works: the CONNECT reply head in `-i`, `-I` and `-D` output. Our build
matched real curl byte for byte (request and stdout) for every case above, run through the
same script with `-Curl`.

Design (implementation choices, no behaviour decision beyond what was measured, so no ADR):
- `Curl.Networking` defines `IConnectReplyHeadWritingEvents : ITransferEvents`; `TcpConnector`
  hands every complete CONNECT reply head (`HttpProxyTunnelReply.Head`) to the target's events
  when they implement it. This stays inside the task's `touches`: no change to
  `Curl.Protocol.Abstractions` or the HTTP handler, which already passes the transfer's events
  on `ConnectTarget.Events`.
- `Curl.Console`'s `TransferContextFactory` wraps the transfer's events in
  `ConnectReplyHeadWritingEvents` (writing to the transfer's header output) only when the
  transfer has an HTTP, HTTP/1.0 or HTTPS proxy, has header output, and
  `--suppress-connect-headers` is off. Limiting it to HTTP-kind proxies keeps every other
  transfer's events untouched.
- A reply curl gives up on (cut short, too large) writes nothing. curl writes header lines
  as it parses them, so a cut-short reply may leave part of a head in curl's output. That
  case was not measured and is left as it is.
- `-v`'s CONNECT lines are other tasks' work (BL-863, BL-964).

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. -i, -I and -D now write the proxy's CONNECT reply head before the response's, and --suppress-connect-headers leaves it out, byte for byte as curl 8.21.0; --proxy1.0 CONNECT pinned as HTTP/1.0
