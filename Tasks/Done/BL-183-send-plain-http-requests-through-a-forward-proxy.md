---
id: BL-183
title: Send plain-HTTP requests through a forward proxy
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-173, BL-162]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-183 — Send plain-HTTP requests through a forward proxy

## Goal

With a forward proxy the handler connects to the proxy and sends an absolute-form request target with Proxy-Authorization and the headers curl adds, while https via a proxy is left to the connector's tunnel.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H15. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `ProxyEndpoint` and `ConnectTarget.Proxy` (BL-162); CONNECT tunnelling is BL-212 in `Curl.Networking`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] An http URL through a proxy sends `GET http://host/path HTTP/1.1` with Proxy-Authorization and any extra proxy headers byte-equal to curl 8.21.0 (measured).
- [x] An https URL through a proxy passes `ConnectTarget.Proxy` to the connector and sends an origin-form request over the returned connection.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H15 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered in `HttpProtocolHandler` (`ForwardProxyOf`, `TargetOf(plan)`, `ProxyAuthorizationFor`) and `HttpRequestHeadFormatter` (`forwardProxy`, `proxyAuthorization`). Tests: `HttpProtocolHandlerTests.Proxy.cs` (1-byte and whole reads) and `HttpRequestHeadFormatterTests.Format_ForwardProxy_*`; new fake `Fakes/OriginAndProxyAuthenticator`.
- Routing (follows ADR-0014): the handler forwards in absolute form only for an `Http`, `Http10` or `Https` kind proxy, an `http` URL and no `-p`; it connects to the proxy itself (`UseTls` for `Https`, no `ConnectTarget.Proxy`). Every other case (https URL, `-p`, SOCKS) goes to the connector as `ConnectTarget.Proxy` with an origin-form request and no proxy headers.
- "Extra proxy headers" in criterion 1 is read, per the Goal, as the headers curl adds itself (`Proxy-Connection: Keep-Alive`). `--proxy-header` has no field in `HttpRequestOptions`, and adding one changes `Curl.Protocol.Abstractions.UnitLibrary`, outside this task's `touches` and claimed by BL-292 in Doing; filed as BL-296.
- The pre-emptive `Proxy-Authorization` comes from the injected authenticator with `IsProxy: true`, Basic, the proxy's credential, no bearer token, and the origin-form request target (curl's Digest `uri=` stays `/a` through a proxy - measured below).
- Measured with `Record-CurlExchange.ps1 -Port 18183` against curl 8.21.0 (mingw, Schannel), 2026-09-26; `-x http://127.0.0.1:18183` on each unless noted:
  - `-U u:p "http://Example.com/a/b?c=d"` -> `GET http://Example.com/a/b?c=d HTTP/1.1
Host: Example.com
Proxy-Authorization: Basic dTpw
User-Agent: curl/8.21.0
Accept: */*
Proxy-Connection: Keep-Alive

`
  - `-x http://pu:pp@127.0.0.1:18183 -H "X-A: 1" -A ua http://example.com:8080/p` -> `GET http://example.com:8080/p HTTP/1.1`, `Host: example.com:8080`, `Proxy-Authorization: Basic cHU6cHA=`, `User-Agent: ua`, `Accept: */*`, `Proxy-Connection: Keep-Alive`, `X-A: 1`.
  - `-u a:b -d xy http://example.com/` -> `POST http://example.com/ HTTP/1.1`, `Host`, `Authorization: Basic YTpi`, `User-Agent`, `Accept`, `Proxy-Connection: Keep-Alive`, `Content-Length: 2`, `Content-Type: application/x-www-form-urlencoded`, body `xy`.
  - `-H "Proxy-Connection: close" -H "Proxy-Authorization: X" -U u:p http://example.com` -> curl's `Proxy-Authorization: Basic dTpw` still sent after `Host`; `Proxy-Connection` replaced; custom lines last in `-H` order.
  - `-U u:p -u a:b "http://x:y@EXample.com:80/A%20b?q#frag"` -> `GET http://EXample.com/A%20b?q HTTP/1.1`, `Host: EXample.com`, `Proxy-Authorization` then `Authorization` (no user info, default port or fragment).
  - `--proxy1.0 127.0.0.1:18183 -I http://example.com/h` -> `HEAD http://example.com/h HTTP/1.1` with `Proxy-Connection: Keep-Alive`: the 1.0 kind changes only CONNECT.
  - `-H "Proxy-Connection:"` -> no `Proxy-Connection` line; `-H "proxy-connection: x"` -> curl's line dropped, custom sent as written.
  - `-H "Host: other" -b a=b -e r --compressed http://example.com/h` -> `... Accept-Encoding: deflate, gzip, br, zstd`, `Referer: r`, `Proxy-Connection: Keep-Alive`, `Cookie: a=b` (Curl pins `deflate, gzip, br` per ADR-0020).
  - `-s -U u:p --digest -u u:p http://127.0.0.1:18184/a -Connections 2` against a Digest 401 -> both requests absolute form with `Proxy-Authorization: Basic dTpw`; the second adds `Authorization: Digest ... uri="/a" ...` after it.
  - `--request-target /rt` via proxy sends `GET /rt`; `--request-target` is not implemented in the handler yet (no task changed that here).
  - `http://[::1]:81/` via proxy: curl 8.21.0 mingw exits 27 `Out of memory` before sending anything, so no IPv6 absolute form was pinned; Curl brackets the host as its `Host` line does.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 264 members, 0 failing (the formatter's `Format` was split, `AppendClientHeaders`, to stay at complexity 10 or below).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Plain-http requests through an HTTP-kind forward proxy go in absolute form with Proxy-Authorization and Proxy-Connection, byte-equal to curl 8.21.0; https, -p and SOCKS go to the connector's tunnel
