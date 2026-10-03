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
completed:
---
# BL-1253 — Write the [TCP] send and recv trace lines over TLS, FTP, proxies and the HAPROXY line

## Goal

Curl writes curl 8.21.0's `[TCP] send(...)` and `[TCP] recv(...)` lines under `--trace-config tcp`, `network`, `all` and `-vvvv` for the connections BL-1195 left untraced: `https://` (the TLS records, below the TLS filter), FTP's control and data connections (`recv(len=900)` on the control connection), connections through a proxy, and the PROXY protocol line (`[TCP] send(len=44) -> 0, 44` after `[SETUP] added HAPROXY filter`).

## Context

- BL-1195 (ADR-0357's BL-1195 amendment) wraps only a direct plain `http://` connection in `TcpIoTraceConnection` (`Curl.Networking.UnitLibrary`), chosen in `TcpConnector.OpenedInPlaintext`.
- Measured already: FTP upload with `-T NUL ftp://127.0.0.1:P/` writes `[TCP] recv(len=900) -> 0, 20` before `< 220 ...` and `[TCP] send(len=16) -> 0, 16` before `> USER anonymous`; `--haproxy-protocol` in BL-1161's Notes. Measure `https://` (handshake records too), a proxy and a `-T` HTTP upload first.
- The `len=` curl reports differs by protocol (102400 for HTTP, 900 for FTP's control reads), so the wrapper needs the length to write per protocol.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (`https://`, an FTP download, a proxy, `--haproxy-protocol`); stderr in Notes.
- [ ] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the `send` and `recv` lines' order beside the `>`/`<` lines for each measured case; ADR-0357 amended.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
