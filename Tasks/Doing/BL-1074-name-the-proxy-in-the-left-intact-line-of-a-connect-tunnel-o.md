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
completed:
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

- [ ] The task's Notes record curl 8.21.0's left-intact line for `-p -x http://...` and
      `--socks5 ...` with `http://` URLs, measured with `Record-CurlExchange.ps1`.
- [ ] `Curl.Protocol.Http.UnitTests` and `Curl.Networking.UnitTests` pin each measured line
      where ours differed.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      every touched `.UnitLibrary`.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
