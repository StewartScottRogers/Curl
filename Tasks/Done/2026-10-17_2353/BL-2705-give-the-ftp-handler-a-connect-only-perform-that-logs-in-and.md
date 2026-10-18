---
id: BL-2705
title: Give the FTP handler a connect-only perform that logs in and keeps the control connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2705 — Give the FTP handler a connect-only perform that logs in and keeps the control connection

## Goal

`FtpProtocolHandler` has a connect-only entry point (as `WsProtocolHandler.ConnectAndUpgradeOnlyAsync` is for WebSocket) that runs only FTP's connect phase - greeting, `AUTH` under `--ssl-reqd`/`ftps`, `USER`, `PASS`, `PWD` - and hands back the open control connection without sending anything more, as libcurl 8.22.0's `CURLOPT_CONNECT_ONLY` 1 does for `ftp://` (`ftp_connect` then `PROTOPT` done).

## Context

Found by BL-2623 (upstream case 597, lib597): Curl's `CURLOPT_CONNECT_ONLY` 1 is a raw TCP connect for every scheme (`ManagedEasyTransfer.IsRawConnectOnly`, `PerformConnectOnlyAsync`), so the server sees nothing; upstream expects `USER anonymous`, `PASS ftp@example.com`, `PWD` on the wire. The engine wiring that calls this entry point is BL-2623's (Curl.Engine.UnitLibrary). Reuse the handler's own login code (`FtpSession`, `FtpControlChannel`) so the commands, replies and failures (`curl: (67)` and the rest) match a normal transfer's connect phase.

## Acceptance criteria

- [x] A public `FtpProtocolHandler` method runs the connect phase on an `ITransferContext` and returns the open control connection (or the transfer's failure), with XML doc comments.
- [x] A test in `Curl.Protocol.Ftp.UnitTests` over a scripted connection pins the bytes sent as `USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n` and nothing after, and the connection is left open.
- [x] A test pins that a refused login answers the same exit code and message as a normal transfer's.
- [x] Curl.Protocol.Ftp.UnitLibrary keeps 100% line and branch coverage and no method past the complexity gate.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Design (mirrors `WsProtocolHandler.ConnectAndUpgradeOnlyAsync`, BL-2471): `FtpProtocolHandler.ConnectOnlyAsync(ITransferContext)` returns `FtpConnectOnlyOutcome(TransferResult Result, IConnection? Connection)`. It shares `RunSessionAsync` with `ExecuteAsync`, so the connect, `-v` lines and failures are the same code; `FtpSession.ConnectOnlyAsync` runs `ConnectAsync` (or resumes a kept connection, sending nothing) and stops. On success the session stops owning AUTH's secured connection and `FtpConnectionLease` leaves the control connection open; on failure the result is the one `RunAsync` gives (refused `USER` = exit 67 `Access denied: 530`, pinned against `ExecuteAsync`) and the connection is closed. The result's report carries the last reply code and entry path.
- Choice: a successful connect-only writes no session-end diagnostic log line (the connection stays open), as the WebSocket one writes none; a failure writes it as a transfer does.
- Choice: bytes the control channel read past the `PWD` reply are not handed back (a server sends none before a command); no `AlreadyReceived` field.
- Measured: Measure-CodeQuality for Curl.Protocol.Ftp.UnitLibrary - 100% line, 100% branch, 0 failing members, worst CRAP 10. `dotnet build` 0 warnings, fast tests green. Tests: `FtpProtocolHandlerConnectOnlyTests` (7). Ran the feature stages in-session (no subagents) to keep the run's cost down.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 347ae2b2 44de5998 7515fd57 6faaa043 3d836d5e.
- 2026-10-17: Doing -> Done. FtpProtocolHandler.ConnectOnlyAsync runs only FTP's connect phase (USER, PASS, PWD) and hands back the open control connection
