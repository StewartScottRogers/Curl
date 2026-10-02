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
completed:
---
# BL-862 — Reuse the proxy connection after a chunked 407 to CONNECT

## Goal

After a `407` to a CONNECT whose body is `Transfer-Encoding: chunked` and which does not close the connection, `TcpConnector` reads and discards the chunked body and sends the authenticated CONNECT on the same connection, as curl 8.21.0 does, instead of dialling the proxy again.

## Context

BL-602 (see `Tasks/Doing/BL-602-answer-a-connect-tunnel-s-407-with-the-proxy-auth-scheme-cho.md`, Notes, or its archived copy once done) and `Documentation/Planning/Decisions/ADR-0186-a-connect-tunnel-answers-a-407-through-the-injected-proxy-authenticator.md` made `TcpConnector` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) answer a CONNECT `407` through `HttpProxyTunnelOptions.ProxyAuthenticator`. The measurements in BL-602's Notes were taken with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1` acting as the proxy; follow the same method.

Today `HttpProxyTunnelReply.LeavesConnectionReusable` (`Curl.Networking.UnitLibrary/HttpProxyTunnelReply.cs`) is false for `Transfer-Encoding: chunked`, so `TcpConnector` dials again; BL-602 Notes record that curl reuses the connection when the `407` does not carry `Connection: close`. The chunked-body discard belongs in `HttpProxyTunnel` (`Curl.Networking.UnitLibrary/HttpProxyTunnel.cs`), which already discards a `Content-Length` body. Tests go in `Curl.Networking.UnitTests/TcpConnectorTests.ProxyAuth.cs` and `HttpProxyTunnelTests.cs`.

## Acceptance criteria

- [ ] Measured with `Record-CurlExchange.ps1` as the proxy: `curl -s -S -p -x http://127.0.0.1:<P> -U u:p --proxy-digest http://example.test/`, the first reply a `407` with `Proxy-Authenticate: Digest realm="r", nonce="abc", qop="auth"`, `Transfer-Encoding: chunked`, a non-empty chunked body and no `Connection: close`, the second `HTTP/1.1 200 Connection established`. The number of connections, both CONNECT requests and the exit code are pinned in this task's Notes with the curl version.
- [ ] `HttpProxyTunnel` reads and discards a chunked `407` body (chunk sizes, chunk extensions and trailers) and leaves the connection positioned at the next reply; an `HttpProxyTunnelTests` case shows it.
- [ ] `HttpProxyTunnelReply.LeavesConnectionReusable` is true for a chunked reply without `Connection: close` (or the replacing member says so by its name); a malformed chunked body is not reused and the failure path returns the same `CurlExitCode` as curl for it, as measured.
- [ ] A `TcpConnectorTests` case with a chunked `407` then `200` shows exactly one dial and both CONNECT requests written to that one connection, matching the measured bytes.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` and `dotnet build Curl.Networking.UnitTests -warnaserror` are clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green across the solution; no new test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
