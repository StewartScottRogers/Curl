---
id: BL-975
title: Name the --connect-to destination in the left intact line as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-900]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-975 — Name the --connect-to destination in the left intact line as curl does

## Goal

A transfer whose connection a `--connect-to` mapping redirected ends its `-v` output with
`* Connection #0 to host <connect-to host>:<connect-to port> left intact`, as curl 8.21.0 does, not the
URL's host and port.

## Context

- Found in BL-900. Measured with `Record-CurlExchange.ps1 -Port 18499 -CurlArgs
  -v,--connect-to,example.invalid:80:127.0.0.1:18499,http://example.invalid/`: curl 8.21.0 ends
  `* Connection #0 to host 127.0.0.1:18499 left intact`. Ours names `example.invalid:80`.
- `--connect-to` is applied inside `Curl.Networking.UnitLibrary` (`ConnectToMappings`, `ConnectDestination`,
  `TcpConnector`), so `HttpProtocolHandler.LeftIntactLine` never learns the destination. One route: the
  connector reports the host and port it dialled on `ConnectResult` (Abstractions), and `LeftIntactLine`
  names that before the alt-svc alternative and the target.
- curl picks the name in the order socks proxy, HTTP proxy, connect-to host, origin; check the proxy
  cases with `Record-CurlExchange.ps1` while here and file a separate task for any that differs.

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests` pins the left-intact line naming the connect-to destination when the
      connector reports one, and `Curl.Networking.UnitTests` pins the connector reporting it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for every touched `.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
