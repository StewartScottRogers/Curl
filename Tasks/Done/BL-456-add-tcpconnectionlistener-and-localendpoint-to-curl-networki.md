---
id: BL-456
title: Add TcpConnectionListener and LocalEndPoint to Curl.Networking for FTP active mode
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-459]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-456 — Add TcpConnectionListener and LocalEndPoint to Curl.Networking for FTP active mode

## Goal

`Curl.Networking` has `TcpConnectionListener`, the production `IConnectionListener` of ADR-0102, and its TCP connections report `LocalEndPoint`, so an FTP transfer in active mode can listen on a port and accept the server's data connection.

## Context

- ADR-0102, "Contract additions", item 2. The contract comes from BL-459.
- `TcpConnector` in `Curl.Networking.UnitLibrary` shows how a socket becomes an `IConnection` and how failures become `ConnectResult.Failed` with curl's exit code and message.
- The listener binds the `ListenTarget` address, trying each port of its range in turn; a range with no free port fails. Take curl 8.21.0's exit code and message for a failed bind (`CURLE_FTP_PORT_FAILED`, 30) from a measurement with `Record-CurlExchange.ps1 -Ftp`, or from `lib/ftp.c` if the case cannot be provoked, and say which under Notes.
- Tests that open a loopback socket are fine as long as they stay fast and platform-neutral; mark any that cannot `TestCategory=Integration`.

## Acceptance criteria

- [x] `TcpConnectionListener.ListenAsync` on `127.0.0.1` port 0 returns a pending connection whose `LocalEndPoint` has a non-zero port, and `AcceptAsync` returns a connection when a client connects to it; pinned by a named test.
- [x] A port range whose every port is taken returns a failed `ListenResult` with the exit code and message recorded under Notes; pinned by a named test.
- [x] The connections `TcpConnector` returns report a non-null `LocalEndPoint`; pinned by a named test.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes on Windows, and no test is Windows-only without an `OSCondition`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for the new members.

## Notes

Filed by BL-437 under ADR-0102. BL-458 depends on this task.

- Plan: ADR-0102 item 2 is the plan. `TcpConnectionListener` binds `ListenTarget.Address` on each
  port from `LowPort` to `HighPort` (0 to 0 is one try on port 0), listens with a backlog of 1 and
  returns an internal `TcpPendingConnection`; its `AcceptAsync` wraps the accepted socket in a
  `StreamConnection`. `StreamConnection` gained an optional `localEndPoint`, `TcpDialer` passes
  the socket's, and `SslStreamConnection` and `PooledConnection` forward the connection
  underneath, so every connection `TcpConnector` (and `PoolingConnector`) returns reports it.
- Measured 2026-09-27 with `Record-CurlExchange.ps1 -Ftp -Port 18456` and
  `-CurlArgs -sS -v -P 127.0.0.1:<port held listening> ftp://127.0.0.1:18456/f.txt`, curl 8.21.0
  (Schannel): exit **30**, `curl: (30) bind() failed, ran out of ports`, after `PWD` and before any
  `EPRT`. Pinned by `TcpConnectionListenerTests.ListenAsync_WhenEveryPortOfTheRangeIsTaken_FailsWithFtpPortFailedAndRanOutOfPorts`.
  No script change was needed (it is BL-437's to extend anyway).
- A failed accept cannot be provoked from the command line, so it comes from curl 8.21.0's
  source (`lib/cf-socket.c`, `cf_tcp_accept_connect`): exit **10** `CURLE_FTP_ACCEPT_FAILED`,
  `Error accept()ing server connect: <strerror>`, not exit 30 as older curl had it. The reason
  is worded by `ConnectFailureReason`, curl's `curlx_strerror` in this code base.
- The other bind and listen failures follow curl 8.21.0's `lib/ftp.c` (source, not provokable
  from the command line on a loopback address): only `EADDRINUSE` and `EACCES` move on to the next
  port; any other bind error is exit 30 `bind(port=<port>) failed: <reason>`, and a socket that
  cannot be opened or put to listening is exit 30 `socket failure: <reason>`. Pinned in
  `TcpConnectionListenerTests` (the non-local case binds TEST-NET-1 `192.0.2.1`, which fails with
  `EADDRNOTAVAIL` on every platform; the socket and listen cases go through internal seams).
- Not done here: curl's once-only retry on the control connection's address after `EADDRNOTAVAIL`
  on a non-local `-P` address. `ListenTarget` holds one address, so it is the FTP handler's or the
  contract's; filed as BL-463.
- Review (code-reviewer) found, and this task fixed: a socket-constructor `SocketException`
  escaping `ListenAsync`, an accepted socket leaked when it failed before being wrapped, and
  every bind error being treated as a taken port.
- Test categories follow `.claude/rules/testing.md` and ADR-0083: tests that only bind local
  sockets run fast (as `UdpDatagramChannelTests` does); the ones that connect
  (`TcpConnectionListenerTests.ListenAsync_OnLoopbackPortZero_AcceptsTheClientThatConnectsAndKeepsItOpenAfterDispose`,
  `TcpConnectorTests.ConnectAsync_OverTheTcpDialer_ReturnsAConnectionThatReportsItsLocalEndPoint`)
  are `Integration`, and the accept-and-wrap step they alone reach,
  `TcpPendingConnection.AcceptStreamConnectionAsync`, carries `[ExcludeFromCodeCoverage]` behind
  the internal `AcceptConnectionAsync` seam. Both pass under a full `dotnet test`.
- Tests: `Curl.Networking.UnitTests` 764 passed, 6 skipped (OpenSSL-build cases), 0 failed with
  Integration included. `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line,
  100% branch, 312 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. TcpConnectionListener listens on a -P port range with curl's exit 30/10 messages, and every TCP connection reports LocalEndPoint
