---
id: BL-862
title: Reuse the proxy connection after a chunked 407 to CONNECT
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-602]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-862 — Reuse the proxy connection after a chunked 407 to CONNECT

## Goal

After a `407` to a CONNECT whose body is `Transfer-Encoding: chunked` and which does not close the connection, `TcpConnector` reads and discards the chunked body and sends the authenticated CONNECT on the same connection, as curl 8.21.0 does, instead of dialling the proxy again.

## Context

BL-602 (see `Tasks/Doing/BL-602-answer-a-connect-tunnel-s-407-with-the-proxy-auth-scheme-cho.md`, Notes, or its archived copy once done) and `Documentation/Planning/Decisions/ADR-0186-a-connect-tunnel-answers-a-407-through-the-injected-proxy-authenticator.md` made `TcpConnector` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) answer a CONNECT `407` through `HttpProxyTunnelOptions.ProxyAuthenticator`. The measurements in BL-602's Notes were taken with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1` acting as the proxy; follow the same method.

Today `HttpProxyTunnelReply.LeavesConnectionReusable` (`Curl.Networking.UnitLibrary/HttpProxyTunnelReply.cs`) is false for `Transfer-Encoding: chunked`, so `TcpConnector` dials again; BL-602 Notes record that curl reuses the connection when the `407` does not carry `Connection: close`. The chunked-body discard belongs in `HttpProxyTunnel` (`Curl.Networking.UnitLibrary/HttpProxyTunnel.cs`), which already discards a `Content-Length` body. Tests go in `Curl.Networking.UnitTests/TcpConnectorTests.ProxyAuth.cs` and `HttpProxyTunnelTests.cs`.

## Acceptance criteria

- [x] Measured with `Record-CurlExchange.ps1` as the proxy: `curl -s -S -p -x http://127.0.0.1:<P> -U u:p --proxy-digest http://example.test/`, the first reply a `407` with `Proxy-Authenticate: Digest realm="r", nonce="abc", qop="auth"`, `Transfer-Encoding: chunked`, a non-empty chunked body and no `Connection: close`, the second `HTTP/1.1 200 Connection established`. The number of connections, both CONNECT requests and the exit code are pinned in this task's Notes with the curl version.
- [x] `HttpProxyTunnel` reads and discards a chunked `407` body (chunk sizes, chunk extensions and trailers) and leaves the connection positioned at the next reply; an `HttpProxyTunnelTests` case shows it.
- [x] `HttpProxyTunnelReply.LeavesConnectionReusable` is true for a chunked reply without `Connection: close` (or the replacing member says so by its name); a malformed chunked body is not reused and the failure path returns the same `CurlExitCode` as curl for it, as measured.
- [x] A `TcpConnectorTests` case with a chunked `407` then `200` shows exactly one dial and both CONNECT requests written to that one connection, matching the measured bytes.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` and `dotnet build Curl.Networking.UnitTests -warnaserror` are clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green across the solution; no new test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

- Measured curl 8.21.0 (x86_64-w64-mingw32, Schannel) 2026-10-01 with `Record-CurlExchange.ps1 -Port 18862 -Connections 2 -HoldOpenMilliseconds 3000 -AnswerHeldRequests 1` as the proxy, `curl -s -S -v -p -x http://127.0.0.1:18862 -U u:p --proxy-digest http://example.test/`, the first reply `HTTP/1.1 407 Proxy Authentication Required` + `Proxy-Authenticate: Digest realm="r", nonce="abc", qop="auth"` + `Transfer-Encoding: chunked`, body `5;ext=1\r\nhello\r\n3\r\nabc\r\n0\r\nX-Trailer: t\r\n\r\n`, no `Connection: close`; the second `HTTP/1.1 200 Connection established`:
  - One connection (`*   Trying 127.0.0.1:18862...` once). On it: `CONNECT example.test:80 HTTP/1.1` / `Host: example.test:80` / `User-Agent: curl/8.21.0` / `Proxy-Connection: Keep-Alive`, then the same with `Proxy-Authorization: Digest username="u",realm="r",nonce="abc",uri="example.test:80",cnonce=...,nc=00000001,response=...,qop="auth"` after `Host`, then the `GET /` inside the tunnel.
  - `-v` after the 407's headers: `* CONNECT responded chunked`, `< `, `* Ignore chunked response-body`, `* chunk reading DONE`, then `* Proxy auth using Digest with user 'u'` and the second CONNECT.
  - Exit 52 (`Empty reply from server`): the recorder answers nothing to the GET inside the tunnel.
- Measured the same way, each body followed by the 200 (exit / CONNECTs sent / stderr):
  - `zz...`: 56 / 1 / `curl: (56) chunk hex-length char not a hex digit: 0x7a` (`-v`: `* closing connection #0`).
  - 17 hex digits: 56 / 1 / `chunk hex-length longer than 16`. `ffffffffffffffff`: 56 / 1 / `invalid chunk size: 'ffffffffffffffff'`.
  - `5\r\nhelloX`, `5\r\nhello\rX`, trailer `A: b\rX`, `0\r\n\rX`: 56 / 1 / `Failure when receiving data from the peer`.
  - Cut short (`5\r\nhel`, then close): 56 / 1 / `Proxy CONNECT aborted`.
  - `5 ;e` (extension after a blank), bare-LF lines, a trailer field: accepted, 2 CONNECTs on one connection.
- Plan and decision: ADR-0333 (amends ADR-0186). New `HttpProxyTunnelChunkedBody` copies `http_chunks.c`'s states as a transition table (a `switch` scored complexity 13-14); `HttpProxyTunnel.DiscardChunkedBodyAsync` reads one byte at a time so the next reply stays on the connection; `HttpProxyTunnelReply.IsChunked` added and `LeavesConnectionReusable` no longer false for chunked; `TcpConnector.DiscardRejectedBodyAsync` picks the discard and turns a chunked failure into exit 56.
- Default taken: curl's 4096-byte trailer line limit is not copied (a long trailer is read through); the `-v` lines above are filed as BL-1144.
- Results: `Curl.Networking.UnitTests` 2420+ passed, full fast suite green; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A chunked 407 to CONNECT is read through and the answer sent on the same connection; a malformed one exits 56 with curl's message
