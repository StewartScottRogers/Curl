---
id: BL-616
title: Send a PROXY protocol v1 header for --haproxy-protocol and --haproxy-clientip
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-612]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-616 — Send a PROXY protocol v1 header for --haproxy-protocol and --haproxy-clientip

## Goal

With `--haproxy-protocol`, the first bytes on the connection are the HAProxy PROXY protocol v1 line (`PROXY TCP4 <src> <dst> <sport> <dport>\r\n`, or `TCP6`), with `--haproxy-clientip` replacing the source address, exactly as curl 8.21.0 writes it, before any TLS or HTTP bytes.

## Context

- Conformance audit 2026-09-28, row 16 (Major). Options: BL-612.
- The line needs the connection's local and remote endpoints (`ConnectResult`), so it belongs in `Curl.Networking.UnitLibrary/TcpConnector.cs` after the dial and before the TLS provider.
- `Record-CurlExchange.ps1` records everything curl sends, so the line shows at the start of `request.bin`.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--haproxy-protocol http://127.0.0.1:<P>/`, the same to `[::1]`, `--haproxy-clientip 1.2.3.4`, and `--haproxy-protocol -k https://...` with `-Tls`; `request.bin` copied into Notes.
- [ ] `Curl.Networking.UnitTests` pin the line for IPv4, IPv6 and a client IP, written before the TLS handshake.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
