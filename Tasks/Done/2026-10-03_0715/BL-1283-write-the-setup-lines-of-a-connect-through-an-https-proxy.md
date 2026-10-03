---
id: BL-1283
title: Write the [SETUP] lines of a connect through an HTTPS proxy
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-03
---
# BL-1283 — Write the [SETUP] lines of a connect through an HTTPS proxy

## Goal

Curl writes curl 8.21.0's `[SETUP]` lines under `--trace-config setup`, `all` and `-vv` and up for a CONNECT tunnel through an HTTPS proxy (`-x https://...`), as it already does through a plain HTTP proxy.

## Context

- Found in BL-1254. `TcpConnector.TracesSetupFilterThrough` leaves `ProxyKind.Https` out, so no `[SETUP]` line is written on that path; `OpenTunnelOverTlsAsync` writes neither added line and sets no `TunnelTrace`.
- Measured 2026-10-02 (curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Tls -Response 'HTTP/1.1 200 Connection established\r\n\r\n'`, `-s -k --proxy-insecure -v --trace-config https-connect,setup -x https://127.0.0.1:18458 https://example.test/`): `[SETUP] happy eyeballing to proxy 127.0.0.1:18458` before `Trying`; after the dial `[SETUP] added SSL filter for HTTP proxy` then `[SETUP] added HTTP proxy tunnel filter` before the proxy's `schannel:` lines; `[SETUP] added SSL filter for origin` after `CONNECT tunnel established`. Measure an `http://` origin through it too (expect `[SETUP] added` first).

## Acceptance criteria

- [x] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the measured `[SETUP]` lines through an HTTPS proxy, for an `https://` and an `http://` origin; ADR-0357 gets an amendment.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Networking.UnitLibrary`.

## Notes

- BL-1255 already did the `Curl.Networking` half: `TracesSetupFilterThrough` is gone, `OpenTunnelOverTlsAsync` writes both added lines and carries an `HttpProxyTunnelTrace`, and `TcpConnectorTests.HttpsProxyTunnelTrace` pins the lines for an `http://` and an `https://` origin. No production change was needed here.
- Added `Curl.Console.UnitTests` pins in `CurlCommandRunnerHttpsConnectTraceTests` (5 cases): `-x https://` to an `https://` origin and to an `http://` origin under `--trace-config setup` and `all`, and no `[SETUP]` line under `proxy` alone. Expected lines are the measured ones (Context; BL-1255 Notes). `-vv` is not pinned separately: it turns on the same `setup` component (ADR-0357), and `all` covers the prefixed-line form.
- ADR-0357 got its BL-1283 amendment. Build `-warnaserror` clean; fast tests all green; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. The [SETUP] lines through an HTTPS proxy are pinned end to end for https:// and http:// origins
