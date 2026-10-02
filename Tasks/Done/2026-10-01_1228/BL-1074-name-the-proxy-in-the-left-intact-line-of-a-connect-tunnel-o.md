---
id: BL-1074
title: Name the proxy in the left intact line of a CONNECT tunnel or SOCKS connection as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-975]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1074 — Name the proxy in the left intact line of a CONNECT tunnel or SOCKS connection as curl does

## Goal

A transfer through a CONNECT tunnel (`-p`, or an `https://` URL through `-x`) or a SOCKS proxy ends
its `-v` output with the `* Connection #0 to host <name>:<port> left intact` line curl 8.21.0
prints, naming whichever host curl names there (curl picks socks proxy, then HTTP proxy, then
connect-to host, then origin).

## Context

- Found in BL-975, which made `TcpConnector` report a `--connect-to` destination on
  `ConnectResult.MappedHost`/`MappedPort` and `HttpProtocolHandler.LeftIntactLine` name it. A
  forward (non-tunnel) `-x http://127.0.0.1:18499 http://example.invalid/` already names the
  proxy, as curl does (measured in BL-975), because the target is the proxy itself.
- Not yet measured: a tunnel that completes, and SOCKS4/SOCKS5. `Record-CurlExchange.ps1`
  closes the connection after its one reply, so a CONNECT tunnel's `GET` fails with exit 56;
  measure with `-Script` (or extend the script) so the tunnel answers both the CONNECT and the
  request, then compare with ours. Ours names `target.Host:target.Port` (the origin) when the
  target carries a `Proxy`.
- If curl names the proxy, the connector can report it the same way BL-975 reports the
  connect-to destination (`ConnectThroughProxyAsync` builds its `DialedSocket` without a
  `MappedDestination`).

## Acceptance criteria

- [x] The task's Notes record curl 8.21.0's left-intact line for `-p -x http://...` and
      `--socks5 ...` with `http://` URLs, measured with `Record-CurlExchange.ps1`.
- [x] `Curl.Protocol.Http.UnitTests` and `Curl.Networking.UnitTests` pin each measured line
      where ours differed.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      every touched `.UnitLibrary`.

## Notes

- Measured curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Script`: the script
  answered the CONNECT (or the SOCKS handshake), then the `GET`, then held the connection
  (`read` with `-ScriptIdleMilliseconds 1500`). No extension of the script was needed.
  - `-v -p -x http://127.0.0.1:18531 http://example.invalid/` →
    `* Connection #0 to host example.invalid:80 left intact` (exit 0)
  - `-sv -p -x http://127.0.0.1:18536 http://example.invalid:8080/` →
    `* Connection #0 to host example.invalid:8080 left intact`
  - `-sv --socks5 127.0.0.1:18535 http://localhost:8080/` →
    `* Connection #0 to host localhost:8080 left intact`
  - `-sv --socks5-hostname 127.0.0.1:18533 http://example.invalid/` →
    `* Connection #0 to host example.invalid:80 left intact`
  - `-sv --socks4 127.0.0.1:18534 http://localhost/` →
    `* Connection #0 to host localhost:80 left intact`
  - (`--socks5` with `http://example.invalid/` fails locally with exit 6, as curl resolves the
    name itself.)
- So curl names the **origin**, not the proxy, through a CONNECT tunnel or a SOCKS proxy; the
  task title's guess was wrong. Ours already did the same: the same five runs through
  `Curl.Console`'s build printed byte-identical lines. No production change was needed, so no
  ADR: this matches measured behaviour rather than choosing one.
- Pinned: `HttpProtocolHandlerTests.ExecuteAsync_KeptAliveThroughATunnelledProxy_ReportsTheOriginLeftIntact`
  (-p via HTTP proxy, SOCKS4, SOCKS5, SOCKS5h) and
  `TcpConnectorTests.ConnectAsync_ThroughATunnel_ReportsNoMappedDestinationSoTheOriginIsNamedLeftIntact`
  / `ConnectAsync_ThroughSocks_ReportsNoMappedDestinationSoTheOriginIsNamedLeftIntact` (SOCKS4,
  SOCKS5). `LeftIntactLine`'s doc comment now says the tunnel and SOCKS case names the origin.
- `Measure-CodeQuality.ps1`: Curl.Protocol.Http.UnitLibrary and Curl.Networking.UnitLibrary both
  100% line, 100% branch, 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Left intact line through a CONNECT tunnel or SOCKS proxy names the origin as curl 8.21.0 does, measured and pinned
