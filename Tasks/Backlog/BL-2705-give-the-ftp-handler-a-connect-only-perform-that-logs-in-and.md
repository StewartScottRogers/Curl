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
completed:
---
# BL-2705 — Give the FTP handler a connect-only perform that logs in and keeps the control connection

## Goal

`FtpProtocolHandler` has a connect-only entry point (as `WsProtocolHandler.ConnectAndUpgradeOnlyAsync` is for WebSocket) that runs only FTP's connect phase - greeting, `AUTH` under `--ssl-reqd`/`ftps`, `USER`, `PASS`, `PWD` - and hands back the open control connection without sending anything more, as libcurl 8.22.0's `CURLOPT_CONNECT_ONLY` 1 does for `ftp://` (`ftp_connect` then `PROTOPT` done).

## Context

Found by BL-2623 (upstream case 597, lib597): Curl's `CURLOPT_CONNECT_ONLY` 1 is a raw TCP connect for every scheme (`ManagedEasyTransfer.IsRawConnectOnly`, `PerformConnectOnlyAsync`), so the server sees nothing; upstream expects `USER anonymous`, `PASS ftp@example.com`, `PWD` on the wire. The engine wiring that calls this entry point is BL-2623's (Curl.Engine.UnitLibrary). Reuse the handler's own login code (`FtpSession`, `FtpControlChannel`) so the commands, replies and failures (`curl: (67)` and the rest) match a normal transfer's connect phase.

## Acceptance criteria

- [ ] A public `FtpProtocolHandler` method runs the connect phase on an `ITransferContext` and returns the open control connection (or the transfer's failure), with XML doc comments.
- [ ] A test in `Curl.Protocol.Ftp.UnitTests` over a scripted connection pins the bytes sent as `USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n` and nothing after, and the connection is left open.
- [ ] A test pins that a refused login answers the same exit code and message as a normal transfer's.
- [ ] Curl.Protocol.Ftp.UnitLibrary keeps 100% line and branch coverage and no method past the complexity gate.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
