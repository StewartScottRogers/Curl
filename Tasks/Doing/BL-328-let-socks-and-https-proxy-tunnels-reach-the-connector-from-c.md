---
id: BL-328
title: Let SOCKS and HTTPS-proxy tunnels reach the connector from Curl.Console once it opens them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-213, BL-266]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-328 — Let SOCKS and HTTPS-proxy tunnels reach the connector from Curl.Console once it opens them

## Goal

`--socks4`, `--socks4a`, `--socks5`, `--socks5-hostname`, `socks*://` and `https://` proxies that need a tunnel reach the connector from `curl http(s)://...` instead of exiting 4.

## Context

- Filed by BL-238 (2026-09-27). `Curl.Console/TransferProxySelection.cs` ends an `http`/`https` transfer with exit 4 `Unsupported proxy '<host>:<port>', Curl cannot tunnel through a <kind> proxy yet` when the route needs a SOCKS tunnel or an HTTPS-proxy tunnel, because `TcpConnector` throws `NotSupportedException` for those kinds (ADR-0053).
- Once BL-213 (SOCKS) and BL-266 (HTTPS proxy) land in `Curl.Networking`, remove `TunnelNotBuiltYetFailure` and its tests in `CurlCompositionProxyTests`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH (ADR-0009) - against a loopback server (`Record-CurlExchange.ps1`), record the exact command and the bytes in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `TransferProxySelection` no longer returns exit 4 for any proxy kind; the exit-4 rows in `CurlCompositionProxyTests` are replaced by rows pinning the measured SOCKS5 greeting (`05 02 00 01` for `--socks5`, BL-192 Notes) and the HTTPS-proxy CONNECT bytes over the fake connector.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library this task changes.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
