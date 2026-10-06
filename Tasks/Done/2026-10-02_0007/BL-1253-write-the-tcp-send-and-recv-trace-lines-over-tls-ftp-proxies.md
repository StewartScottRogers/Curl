---
id: BL-1253
title: Write the [TCP] send and recv trace lines over TLS, FTP, proxies and the HAPROXY line
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1195]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1253 — Write the [TCP] send and recv trace lines over TLS, FTP, proxies and the HAPROXY line

## Goal

Curl writes curl 8.21.0's `[TCP] send(...)` and `[TCP] recv(...)` lines under `--trace-config tcp`, `network`, `all` and `-vvvv` for the connections BL-1195 left untraced: `https://` (the TLS records, below the TLS filter), FTP's control and data connections (`recv(len=900)` on the control connection), connections through a proxy, and the PROXY protocol line (`[TCP] send(len=44) -> 0, 44` after `[SETUP] added HAPROXY filter`).

## Context

- BL-1195 (ADR-0357's BL-1195 amendment) wraps only a direct plain `http://` connection in `TcpIoTraceConnection` (`Curl.Networking.UnitLibrary`), chosen in `TcpConnector.OpenedInPlaintext`.
- Measured already: FTP upload with `-T NUL ftp://127.0.0.1:P/` writes `[TCP] recv(len=900) -> 0, 20` before `< 220 ...` and `[TCP] send(len=16) -> 0, 16` before `> USER anonymous`; `--haproxy-protocol` in BL-1161's Notes. Measure `https://` (handshake records too), a proxy and a `-T` HTTP upload first.
- The `len=` curl reports differs by protocol (102400 for HTTP, 900 for FTP's control reads), so the wrapper needs the length to write per protocol.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (`https://`, an FTP download, a proxy, `--haproxy-protocol`); stderr in Notes.
- [x] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the `send` and `recv` lines' order beside the `>`/`<` lines for each measured case; ADR-0357 amended. (Proxy and `--haproxy-protocol` here; `https://` split to BL-1260 and FTP to BL-1259, see Notes.)
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`, `-s -v --trace-config tcp`; fixtures in `%TEMP%\bl1253\<case>`. Connect-attempt `[TCP]` lines (BL-1161) omitted.
  - `--haproxy-protocol http://127.0.0.1:18531/`: `[TCP] send(len=44) -> 0, 44`, `Established connection ...`, `[TCP] query ALPN`, `using HTTP/1.x`, `[TCP] send(len=79) -> 0, 79`, the `>` lines, `Request completely sent off`, `[TCP] recv(len=102400) -> 81, 0`, `[TCP] recv(len=102400) -> 0, 43`, the `<` lines.
  - `-x http://127.0.0.1:18533 http://example.invalid/`: as a direct connection, with `[TCP] send(len=131) -> 0, 131` before `> GET http://example.invalid/ HTTP/1.1` ... `> Proxy-Connection: Keep-Alive`.
  - `-k https://127.0.0.1:18532/`: after `ALPN: curl offers http/1.1`, `[TCP] send(len=429) -> 0, 429`, `[TCP] recv(len=4096) -> 81, 0`, `[TCP] adjust_pollset, !active, POLLIN fd=440`, `[TCP] recv(len=4096) -> 0, 1175`, `[TCP] send(len=158) -> 0, 158`, `adjust_pollset` again, `[TCP] recv(len=4096) -> 0, 51`, `ALPN: server did not agree on a protocol. Uses default.`, `Established connection`, no `query ALPN`, `using HTTP/1.x`, `[TCP] send(len=108) -> 0, 108`, the `>` lines, `[TCP] recv(len=103424) -> 81, 0`, `[TCP] recv(len=103424) -> 0, 72`, the `<` lines.
  - `ftp://127.0.0.1:18534/a.txt` (5 bytes): `[TCP] recv(len=900) -> 0, <n>` before each `<` reply and `[TCP] send(len=<n>) -> 0, <n>` before each `>` command; the data connection writes `[TCP-1] recv(len=5) -> 81, 0`, `[TCP-1] recv(len=5) -> 0, 5`, then `[TCP-1] cf_socket_shutdown, fd=216`, `shut down successfully`, `destroy`, `cf_socket_close`; `[TCP] recv(len=900) -> 81, 0` comes before the `226`.
- Delivered (ADR-0357's BL-1253 amendment): `TcpConnector.WriteHaproxyLineAsync` writes `[TCP] send(len=<n>) -> 0, <n>` after the PROXY line under `TracesTcpFilter`, before `Established connection`, over TLS too. The forward proxy already matched (its target carries `PoolScheme` `http`), so it is pinned by a test only.
- Decision: https and FTP split out. FTP's control and data targets look the same to `TcpConnector` (no `PoolScheme`), so telling them apart needs `Curl.Protocol.Ftp.UnitLibrary` and `ConnectTarget` (`Curl.Protocol.Abstractions.UnitLibrary`, a shared contract), outside `touches`; https needs the TLS record layer's read sizes (Schannel's 4096 and 103424) and a decision on which record sizes can match. Filed BL-1259 (FTP) and BL-1260 (https).
- Tests: `TcpConnectorTests.ConnectAsync_TracingTheTcpFilterWithHaproxy_WritesTheLinesSendBeforeTheConnectionOpened` (http and https), `CurlCommandRunnerTcpIoTraceTests.RunAsync_ThroughAForwardProxyUnderTraceConfigTcp_...` and `RunAsync_WithHaproxyProtocolUnderTraceConfigTcp_...`. `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members. `dotnet build Curl.slnx -warnaserror` clean; fast tests green (0 failed).
- The Debug `curl.exe` was not run through `Record-CurlExchange.ps1 -Curl`: this session may not read under `bin\`.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. curl's [TCP] send line for the --haproxy-protocol line and the [TCP] I/O lines through a forward proxy are written and pinned; https and FTP filed as BL-1260 and BL-1259
