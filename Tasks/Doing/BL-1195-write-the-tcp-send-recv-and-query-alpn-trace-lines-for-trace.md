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
completed:
---
# BL-1195 — Write the [TCP] send, recv and query ALPN trace lines for --trace-config tcp, network, all and -vvvv

## Goal

Curl writes curl 8.21.0's `[TCP]` lines for the connection's I/O - `[TCP] query ALPN` before `using HTTP/1.x`, `[TCP] send(len=<n>) -> 0, <n>` before each request's `>` lines, `[TCP] recv(len=102400) -> <result>, <bytes>` before the response - under `--trace-config tcp`, `network`, `all` and `-vvvv`.

## Context

- Split from BL-1161 (ADR-0357), which delivered the `[TCP]` lines of the connect attempts (`ConnectAttemptTraceEvents` in `Curl.Networking.UnitLibrary`, under `TcpConnector.TracesTcpFilter`). BL-1161's Notes hold curl's measured stderr: for a 40-byte response, `recv(len=102400) -> 81, 0` (CURLE_AGAIN, nothing yet) then `-> 0, 40`; with `--haproxy-protocol`, `[TCP] send(len=44) -> 0, 44` after `[SETUP] added HAPROXY filter`.
- A wrapping `IConnection` over the dialled socket is the likely seam; `query ALPN` is asked by the HTTP handler as it picks the HTTP version. Measure a larger body (several `recv` lines) and an upload first.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (plain GET, a body larger than 102400 bytes, a `-d` upload); stderr in Notes.
- [ ] Tests pin the `query ALPN`, `send` and `recv` lines' order beside the `>`/`<` lines for a plain GET, and that none appears without `tcp`, `network` or `all`; ADR-0357 amended for how `recv`'s would-block results are produced.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
