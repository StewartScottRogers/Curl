---
id: BL-754
title: Reuse a connection across -:/--next option groups when their TLS and proxy settings match
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-509]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-754 — Reuse a connection across -:/--next option groups when their TLS and proxy settings match

## Goal

A later `-:`/`--next` option group reuses an idle connection an earlier group left in the pool when curl 8.21.0 would (same host, port, scheme, proxy and matching TLS configuration), so `-v` prints `Re-using existing connection` and `%{num_connects}` is `0` for it, as upstream does.

## Context

- BL-509 / ADR-0126: each option group gets its own `TransferDispatch`, so its own `PoolingConnector`; connections are never reused across groups. curl 8.21.0 shares one connection cache across all groups (the tool's share handle locks `CURL_LOCK_DATA_CONNECT`) and reuses a connection only when `Curl_ssl_config_matches` and the proxy settings agree.
- Measured in BL-509 Notes: `-w '[%{conn_id} %{num_connects}]' A --next -w ... B` against a server that kept the connection open sent `GET /b` on A's connection first.
- `Curl.Networking.UnitLibrary/ConnectionPoolKey.cs` leaves the TLS options out of the key because "one run has one set"; with groups that is no longer true. `Curl.Console/CurlComposition.CreateTransports` builds the `TcpConnector` from one group's options.
- Start: one run-wide pool whose key carries the TLS and proxy-tunnel configuration, and a per-group inner connector.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -HoldOpenMilliseconds` (extend it to answer a second request on a held connection if needed): same settings in both groups, and `-k` in only one; `-v` lines, `%{num_connects}` and the request bytes copied into Notes.
- [ ] `Curl.Console.UnitTests` pin that a second group with the same settings reuses the first group's connection and one with a different TLS setting opens a new one.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Console` and `Curl.Networking.UnitLibrary` and no failing member.

## Notes

## Log

- 2026-09-28: Created.
