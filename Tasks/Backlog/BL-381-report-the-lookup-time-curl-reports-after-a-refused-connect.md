---
id: BL-381
title: Report the lookup time curl reports after a refused connect
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-381 — Report the lookup time curl reports after a refused connect

## Goal

`%{time_namelookup}` after a refused connect prints the lookup time, as curl 8.21.0 prints it, and `ConnectTimings.NameResolved`'s doc comment says what `TcpConnector` does.

## Context

- Found by BL-287 (ADR-0073, "Consequences"). Measured on curl 8.21.0 (mingw, Schannel), 2026-09-27: `curl -s -o NUL -w "ns=%{time_namelookup}|c=%{time_connect}" http://127.0.0.1:1/` printed `ns=0.000048|c=0.000000`, exit 7; for `http://nonexistent.invalid/` (exit 6) `ns=0.000000`. Ours prints `ns=0.000000` for both.
- `ConnectResult.Failed` carries no timings, so `TcpConnector` cannot report the `NameResolved` it took before the dial failed, and `HttpProtocolHandler`'s failed-connect report (`FailedConnectReport`) has no `Connect`. Give a failed result the connector's timings (e.g. a `Failed` overload or `with`-style init), have `TcpConnector` pass `Started` and `NameResolved`, and have the handler copy them. `time_connect` must stay `0.000000` (measured), and `ConnectTimings.Connected` is not nullable today, so decide how a failed connect leaves it unset.
- `ConnectTimings.NameResolved` still documents `null` for a literal address, but `TcpConnector` always sets it (ADR-0030).

## Acceptance criteria

- [ ] A `TcpConnectorTests` test pins a refused dial's result carrying `Started` and `NameResolved`, and an `HttpProtocolHandlerTests` test pins a failed connect's report carrying them, with `time_connect` still `0`.
- [ ] `ConnectTimings.NameResolved`'s doc comment no longer says it is `null` for a literal address.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-27: Created.
