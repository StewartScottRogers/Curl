---
id: BL-1449
title: Fail with exit 56 Recv failure instead of crashing when the proxy drops a CONNECT reply read
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: FR-091
created: 2026-10-04
completed:
---
# BL-1449 — Fail with exit 56 Recv failure instead of crashing when the proxy drops a CONNECT reply read

## Goal

When the proxy drops the connection while Curl reads a CONNECT reply - as after a `407` with no `Content-Length` whose connection the proxy closed, so the next CONNECT is sent into a dead socket - Curl ends with curl 8.21.0's `curl: (56) Recv failure: <socket error words>` instead of crashing with an unhandled `System.IO.IOException` (exit -532462766 and a .NET stack trace on standard error).

## Context

- Reproduce (2026-10-04, Windows): `Record-CurlExchange.ps1 -Connections 3 -Response @('HTTP/1.1 407 Need\r\nProxy-Authenticate: Basic realm="x"\r\n\r\n','HTTP/1.1 200 OK\r\n\r\n') -CurlArgs @('-v','-m','5','-p','-x','http://127.0.0.1:<port>','--proxy-anyauth','-U','u:p','http://example.test/')` (run from PowerShell with `&` so the arrays survive). The recorder closes each connection after its response. Real curl 8.21.0 sends the second CONNECT on the same connection, then writes `* Recv failure: Connection was reset`, `* closing connection #0` and `curl: (56) Recv failure: Connection was reset`, exit 56. `Curl.Console` sends the same second CONNECT and then dies: `Unhandled exception. System.IO.IOException: Unable to read data from the transport connection: An established connection was aborted ...` from `HttpProxyTunnel.ReadReplyAsync` (`Curl.Networking.UnitLibrary/HttpProxyTunnel.cs` line 162), called from `TcpConnector.RequestTunnelAsync` (`TcpConnector.cs` line 1837).
- `HttpProxyTunnel` already catches `IOException` while it reads a reply *body* (near line 375), but not while it reads the head. A reset that arrives after part of a head was read already ends as `curl: (56) Proxy CONNECT aborted` in both curl and Curl (measured with `-ResetAfterResponse`), so only the read that fails before or between replies is missing.
- Upstream (tag `curl-8_21_0`): `lib/cf-h1-proxy.c` reads the reply through the socket filter, whose receive failure is `Recv failure: <strerror>` (`lib/cf-socket.c`), `CURLE_RECV_ERROR`. `Curl.Protocol.Abstractions.UnitLibrary/CurlSocketErrorText.cs` (BL-1325) already words a failed read the way the platform's curl build does; use it rather than a new table.
- The words depend on the socket error the OS reports (the measured run got WSAECONNRESET in curl and WSAECONNABORTED in Curl, a timing difference); pin the mapping from the error, not the race.

## Acceptance criteria

- [ ] A test in `Curl.Networking.UnitTests` drives a CONNECT whose reply read throws an `IOException` wrapping a `SocketException` (connection reset, and connection aborted) before any reply byte, and asserts the tunnel fails with `CurlExitCode.RecvError` and `CurlSocketErrorText`'s `Recv failure: ...` message for that error - no exception escapes.
- [ ] A test pins the same for the second CONNECT of a proxy-authentication retry, the case reproduced above, and that a reply read that fails after part of a head still gives `Proxy CONNECT aborted`.
- [ ] Re-running the reproduction above against `Curl.Console` exits 56 with a `curl: (56) Recv failure: ...` line and no stack trace (record the output in Notes).
- [ ] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean; `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
