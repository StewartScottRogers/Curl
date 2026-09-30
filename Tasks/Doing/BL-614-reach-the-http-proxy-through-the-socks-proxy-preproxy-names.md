---
id: BL-614
title: Reach the HTTP proxy through the SOCKS proxy --preproxy names
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-612]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-614 — Reach the HTTP proxy through the SOCKS proxy --preproxy names

## Goal

With `--preproxy socks5://...` and `-x http://...`, the connector reaches the HTTP proxy through the SOCKS proxy (SOCKS handshake to the HTTP proxy's address, then the HTTP proxy's `CONNECT` or forwarded request), as curl 8.21.0 does, with each hop's failure mapped to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 16 (Major). Options: BL-612.
- Code: `Curl.Networking.UnitLibrary/SocksProxyTunnel.cs`, `Socks4Handshake.cs`, `Socks5Handshake.cs` (ADR-0084), `HttpProxyTunnel.cs`, `TcpConnector.cs`; proxy selection in `Curl.Core.UnitLibrary/ProxySelector.cs` and `Curl.Console/TransferProxySelection.cs`. The pool key must include the pre-proxy.
- `Record-CurlExchange.ps1` serves HTTP only; a SOCKS5 no-auth handshake is a fixed byte exchange, so measure with a scripted exchange (BL-532's `-Script` mode if it has landed, otherwise extend the script) and record the bytes.

## Acceptance criteria

- [ ] Measured first as above: `--preproxy socks5://127.0.0.1:<S> -x http://10.0.0.1:<P> http://h/` (request bytes on the SOCKS side), and the SOCKS proxy refusing; stderr and exit code copied into Notes.
- [ ] `Curl.Networking.UnitTests` pin the chained handshake bytes and each hop's failure through fake dialers.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
