---
id: BL-1195
title: Write the [TCP] send, recv and query ALPN trace lines for --trace-config tcp, network, all and -vvvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1161]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1195 — Write the [TCP] send, recv and query ALPN trace lines for --trace-config tcp, network, all and -vvvv

## Goal

Curl writes curl 8.21.0's `[TCP]` lines for the connection's I/O - `[TCP] query ALPN` before `using HTTP/1.x`, `[TCP] send(len=<n>) -> 0, <n>` before each request's `>` lines, `[TCP] recv(len=102400) -> <result>, <bytes>` before the response - under `--trace-config tcp`, `network`, `all` and `-vvvv`.

## Context

- Split from BL-1161 (ADR-0357), which delivered the `[TCP]` lines of the connect attempts (`ConnectAttemptTraceEvents` in `Curl.Networking.UnitLibrary`, under `TcpConnector.TracesTcpFilter`). BL-1161's Notes hold curl's measured stderr: for a 40-byte response, `recv(len=102400) -> 81, 0` (CURLE_AGAIN, nothing yet) then `-> 0, 40`; with `--haproxy-protocol`, `[TCP] send(len=44) -> 0, 44` after `[SETUP] added HAPROXY filter`.
- A wrapping `IConnection` over the dialled socket is the likely seam; `query ALPN` is asked by the HTTP handler as it picks the HTTP version. Measure a larger body (several `recv` lines) and an upload first.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (plain GET, a body larger than 102400 bytes, a `-d` upload); stderr in Notes.
- [x] Tests pin the `query ALPN`, `send` and `recv` lines' order beside the `>`/`<` lines for a plain GET, and that none appears without `tcp`, `network` or `all`; ADR-0357 amended for how `recv`'s would-block results are produced.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`, `-s -v --trace-config tcp`; fixtures in `%TEMP%\bl1195\<case>`. Connect-attempt `[TCP]` lines (BL-1161) omitted.
  - Plain GET, 40-byte response: `Established`, `[TCP] query ALPN`, `using HTTP/1.x`, `[TCP] send(len=79) -> 0, 79`, the `>` lines, `Request completely sent off`, `[TCP] recv(len=102400) -> 81, 0`, `[TCP] recv(len=102400) -> 0, 79`, the `<` lines, `{ [40 bytes data]`, left intact.
  - 150000-byte body (`-o NUL`): as above to `Request completely sent off`, then `[TCP] recv(len=102400) -> 0, 102400` (no would-block), the `<` lines, `{ [102357 bytes data]`, `[TCP] recv(len=47643) -> 0, 47643`, `{ [47643 bytes data]`.
  - `-d hello=world`: `[TCP] send(len=160) -> 0, 160` (head and body in one send) before the `>` lines, `} [11 bytes data]`, `upload completely sent off: 11 bytes`, `[TCP] recv(len=102400) -> 0, 40` (no would-block).
  - `network`: `query ALPN` comes after `[DNS] removing`/`destroy` and `[HAPPY-EYEBALLS] removing`/`destroy`, immediately before `using HTTP/1.x`.
  - FTP (`-T NUL ftp://...`, for the follow-up): no `query ALPN`; `[TCP] recv(len=900) -> 0, 20` before `< 220 ...`, `[TCP] send(len=16) -> 0, 16` before `> USER anonymous`.
- Delivered (ADR-0357's BL-1195 amendment): `TcpIoTraceConnection` (Networking) wraps a direct plain `http://` connection (`PoolScheme` `http`) dialled under `TracesTcpFilter`; `TcpConnector.OpenedInPlaintext` wraps it and writes `QueryAlpnLine` after the setup filters' removal. No change to the HTTP handler or `Curl.Console` production code was needed: the handler reports its head after writing it, and `CurlComposition` already sets `TracesTcpFilter` for `tcp`, `network`, `all` and `-vvvv`.
- Decisions: would-block written when the inner read does not complete at once; `len=` always 102400; TLS, FTP, proxies and the HAPROXY `send` left to BL-1253. A live run of the Debug `curl.exe` through `Record-CurlExchange.ps1 -Curl` printed the plain GET's lines exactly as curl did, would-block included.
- Tests: `TcpIoTraceConnectionTests` (send, recv, would-block, pass-through), `TcpConnectorTests.ConnectAttemptTrace` (plain http wrapped and `query ALPN` written; no scheme and `ftp` untraced), `CurlCommandRunnerTcpIoTraceTests` (full order for `tcp` and `network`; the three lines under `all` and `-vvvv`; none under `-v`, `-vvv` and the other components). `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members.
- Filed BL-1253 for the untraced cases.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config tcp, network, all and -vvvv write curl's [TCP] query ALPN, send and recv lines for a plain HTTP transfer
