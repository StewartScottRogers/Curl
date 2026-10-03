---
id: BL-1259
title: Write the [TCP] send and recv trace lines for an FTP control and data connection
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1253]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1259 — Write the [TCP] send and recv trace lines for an FTP control and data connection

## Goal

Under `--trace-config tcp`, `network`, `all` and `-vvvv`, an `ftp://` transfer writes curl 8.21.0's `[TCP] send(len=<n>) -> 0, <n>` before each `>` command and `[TCP] recv(len=900) -> 0, <n>` before each `<` reply on the control connection, and `[TCP-1] recv(len=<remaining>) ...` lines on the data connection.

## Context

- Measured in BL-1253's Notes (`-s -v --trace-config tcp ftp://127.0.0.1:P/a.txt`, 5-byte file): control reads are `recv(len=900)`, with a would-block `recv(len=900) -> 81, 0` before the final `226`; the data connection writes `[TCP-1] recv(len=5) -> 81, 0`, `[TCP-1] recv(len=5) -> 0, 5` (len = bytes still expected), then `[TCP-1] cf_socket_shutdown`, `shut down successfully`, `destroy`, `cf_socket_close`. Measure an upload (`-T`) and a listing too.
- `TcpIoTraceConnection` (`Curl.Networking.UnitLibrary`, ADR-0357's BL-1195 and BL-1253 amendments) is chosen in `TcpConnector.OpenedInPlaintext` by `PoolScheme` `http`. FTP's control target (`FtpProtocolHandler`) and data target (`FtpSession`) carry no `PoolScheme`, so `ConnectTarget` needs a way for the handler to say which connection it is and what receive length to report.

## Acceptance criteria

- [ ] Upload and listing measured with `Record-CurlExchange.ps1`; stderr in Notes.
- [ ] Tests in `Curl.Networking.UnitTests`, `Curl.Protocol.Ftp.UnitTests` and `Curl.Console.UnitTests` pin the control connection's `send`/`recv(len=900)` lines beside `>`/`<` and the data connection's `[TCP-1]` lines; ADR-0357 amended.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
