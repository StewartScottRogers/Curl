---
id: BL-469
title: Number a failed connect in ConnectResult.Failed and the connectors
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-453]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-469 — Number a failed connect in ConnectResult.Failed and the connectors

## Goal

A failed connect carries curl's number for the connection it tried, so `-v` prints `closing connection #N` with the next number after earlier connections in the same run, as curl 8.21.0 does.

## Context

- ADR-0105 (BL-453): `HttpProtocolHandler` reports `closing connection #N` after a failed connect with `N` from `ConnectResult.ConnectionNumber`, which is `0` for every failure because `ConnectResult.Failed` takes no number.
- curl numbers every connection it creates, failed ones included: a redirect to a refused port after connection `#0` closes `#1`.
- `TcpConnector` (`_nextConnectionNumber`) and `PoolingConnector` number only connections that open.

## Acceptance criteria

- [ ] `ConnectResult.Failed` (and `Refused`) accept a connection number; a test in `Curl.Protocol.Abstractions.UnitTests` shows it on `ConnectionNumber`.
- [ ] A test in `Curl.Networking.UnitTests` shows `TcpConnector` giving a refused connect after one successful connect number `1`.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage for both libraries.

## Notes

- Filed from BL-453 (2026-09-27).

## Log

- 2026-09-27: Created.
