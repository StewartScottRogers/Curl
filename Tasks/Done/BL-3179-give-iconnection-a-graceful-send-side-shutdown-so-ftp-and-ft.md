---
id: BL-3179
title: Give IConnection a graceful send-side shutdown so FTP and FTPS data connections end with FIN and close_notify
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/IConnection.cs, Curl.Networking.UnitLibrary/DialedTcpConnection.cs, Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Networking.UnitLibrary/StreamConnection.cs, Curl.Networking.UnitLibrary/SslStreamConnection.cs, Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs, Curl.Networking.UnitLibrary/TcpIoTraceConnection.cs, Curl.Networking.UnitTests/StreamConnectionShutDownSendingTests.cs, Curl.Networking.UnitTests/TlsConnectionShutDownSendingTests.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerDataShutdownTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3179 — Give IConnection a graceful send-side shutdown so FTP and FTPS data connections end with FIN and close_notify

## Goal

Closing an FTP or FTPS data connection sends TLS close_notify (on a TLS data connection), shuts the TCP send side down (FIN) and never resets the connection over unread bytes, as curl does before it reads the end-of-transfer reply.

## Context

Follow-up of BL-3161, which moved the data-connection close ahead of the end-of-transfer reply in `FtpSession.ReadTransferCompleteAsync` (the test.rebex.net 426). `IConnection` has only `DisposeAsync`: a TLS data connection is disposed without `close_notify`, and a socket closed with bytes still unread (a server's `close_notify` after the last data byte, when the download stops at the `SIZE` count) makes the OS send RST, which servers such as Rebex report as 426 "unable to close data connection gracefully". Measure curl 8.22.0 against an FTPS server with `Record-CurlExchange.ps1 -Ftp -Tls` before pinning the order. Start at `Curl.Protocol.Abstractions.UnitLibrary/IConnection.cs` (a default-implemented shutdown member, as `ClearTlsAsync` is) and `Curl.Networking.UnitLibrary/DialedTcpConnection.cs`.

## Acceptance criteria

- [x] `IConnection` has a default-implemented member that ends the send side gracefully, and the TCP and TLS connections implement it (TLS: close_notify, then TCP shutdown of the send side).
- [x] `FtpSession` calls it on the data connection before disposing it, ahead of the end-of-transfer reply, with a unit test in `Curl.Protocol.Ftp.UnitTests`.
- [x] The fast tests pass and every changed library meets the quality gates.

## Notes

- Touches widened: the TCP connection is `StreamConnection` (DialedTcpConnection only carries it, so it is unchanged), the TLS connections are `SslStreamConnection` and `HandBuiltTlsConnection`, and `TcpIoTraceConnection` forwards; plus three new test files. No task in Doing on origin/work/dark-factory names any of them.
- Decision (ADR-0770): `IConnection.ShutDownSendingAsync`, default no-op. TLS sends close_notify then calls the plaintext one; `StreamConnection` over a `NetworkStream` discards `Socket.Available` unread bytes, then `Shutdown(Send)`, ignoring `SocketException` (an ADR-0083 adapter behind the `ShutDownSendSide` seam). `FtpSession.CloseDataConnectionAsync` shuts down, ignoring `IOException`, then disposes, before the end reply. Other wrappers (PooledConnection, EarlyDataTlsConnection, Http2ProxyTunnelConnection, SocketCallbackConnection) keep the default: an FTP data connection does not go through them.
- Not measured with Record-CurlExchange.ps1 -Ftp -Tls: its transcript does not show the TCP shutdown and no output text changes; order taken from the task and curl's OpenSSL close (SSL_shutdown, then close). Recorded in ADR-0770.
- Verified: dotnet build clean; fast tests green in all 44 test projects; Measure-CodeQuality for Abstractions, Networking and Ftp: 0 failing members.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: c8dde9bd 617e8d20 965dd23c.
- 2026-10-22: Doing -> Done. FTP and FTPS data connections end with close_notify and FIN before the end-of-transfer reply
