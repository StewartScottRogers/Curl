---
id: BL-866
title: Keep an h2c-upgraded connection for the transfer's later requests and report it left intact
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-716]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-866 — Keep an h2c-upgraded connection for the transfer's later requests and report it left intact

## Goal

After `--http2` upgrades a cleartext connection to h2c, the transfer's later requests on it (a 401 retry, a followed redirect to the same origin, the next URL) go out as new HTTP/2 streams (3, 5, ...) on that connection, and `-v` ends with `Connection #0 to host H:P left intact` as curl prints it.

## Context

- BL-716 made the upgrade work for one exchange: `HttpH2cUpgradeConnection` switches the exchange to stream 1 after a `101`. Because the head reader sees a `101`, `HttpResponseHeadReader.SwitchedProtocols` makes `HttpProtocolHandler.DeliveredWhole` false. So the connection is never reused, a retry or same-origin redirect goes out on a fresh connection, and `-v` ends with the shutting-down line rather than curl's `left intact`.
- Measured in BL-716's Notes: curl.se's nghttp2 curl 8.18.0 (`%LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe`) prints `* Connection #0 to host 127.0.0.1:48717 left intact` after the upgraded response.
- Measure curl first with `Record-CurlExchange.ps1 -Curl <that curl> -Connections 1 -HoldOpenMilliseconds ...`: two URLs on one command line after an upgrade, and a 401 answered over the upgraded stream with `--anyauth`/`-u`.
- Likely shape: once the upgrade connection switches, hand its `Http2Session` to `ExchangeOnConnectionAsync` as the connection's `IHttpStreamSession`, so later exchanges and `KeepsAlive` take the existing HTTP/2 path.

## Acceptance criteria

- [ ] curl measured first as above; request bytes and stderr copied into Notes.
- [ ] `Curl.Protocol.Http.UnitTests` pin, through a fake connection, a 401 answered on the upgraded stream 1 being retried as HEADERS on stream 3 of the same connection, and the `left intact` line after an upgraded exchange.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
