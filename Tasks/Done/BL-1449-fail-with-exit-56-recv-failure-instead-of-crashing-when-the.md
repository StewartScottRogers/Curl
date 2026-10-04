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
completed: 2026-10-04
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

- [x] A test in `Curl.Networking.UnitTests` drives a CONNECT whose reply read throws an `IOException` wrapping a `SocketException` (connection reset, and connection aborted) before any reply byte, and asserts the tunnel fails with `CurlExitCode.RecvError` and `CurlSocketErrorText`'s `Recv failure: ...` message for that error - no exception escapes.
- [x] A test pins the same for the second CONNECT of a proxy-authentication retry, the case reproduced above, and that a reply read that fails after part of a head still gives `Proxy CONNECT aborted`.
- [x] Re-running the reproduction above against `Curl.Console` exits 56 with a `curl: (56) Recv failure: ...` line and no stack trace (record the output in Notes).
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean; `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Notes

- `HttpProxyTunnel.ReadReplyAsync` now catches an `IOException` that holds a `SocketException` (found by `CurlSocketErrorText.ReceiveFailure`). Before the reply's first byte it returns exit 56 `Recv failure: <words>`. After part of a head it returns `Proxy CONNECT aborted`, the same as a close at that point. Decision (sensible default): an `IOException` with no socket error inside still escapes as before, so the existing "reading the reply throws, dispose and rethrow" tests keep pinning that path and an unexpected failure stays visible.
- Real curl also writes `* Recv failure: Connection was reset` as a `-v` line before `* closing connection #0`. `ConnectTunnelVerboseLines.ReportReplyFailure` now writes a `Recv failure: ...` failure as an info line.
- Measured: real curl also writes `* Proxy CONNECT aborted` for a head cut short (`-ResetAfterResponse`) and Curl does not. That gap predates this task; filed as BL-1454.
- `ScriptedConnection` gained `ExceptionAfterScript`: the exception a read throws once the scripted bytes run out, so a test can serve a 407 and then a reset.
- Reproduction against `Curl.Console` (Debug build) after the fix: exit 56, no stack trace. Last lines of standard error:
  ```
  * Recv failure: Connection was aborted
  * closing connection #0
  curl: (56) Recv failure: Connection was aborted
  ```
  Real curl in the same setup: `* Recv failure: Connection was reset`, `* closing connection #0`, `curl: (56) Recv failure: Connection was reset`, exit 56. The words differ only in which socket error the OS reports (the race the Context describes).
- Tests: Curl.Networking.UnitTests 3028 passed, 28 skipped; whole-solution fast run green. `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Done. A CONNECT reply read that fails with a socket error now ends curl: (56) Recv failure: <words> instead of crashing
