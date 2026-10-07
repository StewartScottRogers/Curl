---
id: BL-1246
title: Write the [SOCKS] query ALPN trace line after a SOCKS connection is established
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1195]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0357-vv-and-trace-config-setup-write-the-setup-filter-lines.md]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1246 — Write the [SOCKS] query ALPN trace line after a SOCKS connection is established

## Goal

Curl writes curl 8.21.0's `[SOCKS] query ALPN` line after `Established connection` and before `using HTTP/1.x` for a plain HTTP transfer through a SOCKS proxy or `--preproxy` under `--trace-config socks`, `proxy` and `all`.

## Context

- Split from BL-1191 (ADR-0357's BL-1191 amendment), which wrote every other `[SOCKS]` line. The line is the HTTP layer asking the filter chain for ALPN, so it belongs beside `[TCP] query ALPN` (BL-1195), which this depends on.
- Measured in BL-1191 Notes: `* [SOCKS] query ALPN` right before `* using HTTP/1.x`. Measure first whether it repeats on a reused connection, and whether an `https://` target through SOCKS writes it (the TLS filter likely answers instead).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Script` (plain HTTP, a reused connection, an https target), stderr in Notes.
- [x] Tests in `Curl.Networking.UnitTests` or `Curl.Console.UnitTests` pin the line's place, and that it is absent without `socks`, `proxy` or a named `all`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

Measured 2026-10-02 with `Record-CurlExchange.ps1 -Script` playing a SOCKS5h proxy (curl 8.21.0 mingw, Schannel); fixtures in `%TEMP%\bl1246\<case>`.

- Plain HTTP, two URLs on one connection (`-s -v --trace-config socks -x socks5h://127.0.0.1:18611 http://example.test/a http://example.test/b`), exit 0:

```
* [SOCKS] SOCKS5 request granted.
* Opened SOCKS connection from 127.0.0.1 port 64267 to example.test port 80 (via 127.0.0.1 port 18611)
* Established connection to 127.0.0.1 (127.0.0.1 port 18611) from 127.0.0.1 port 64267 
* [SOCKS] query ALPN
* using HTTP/1.x
> GET /a HTTP/1.1
...
* Connection #0 to host example.test:80 left intact
* Reusing existing http: connection with proxy 127.0.0.1
> GET /b HTTP/1.1
```

  The reused connection writes no second `query ALPN`.
- `--trace-config network`: `[TCP] send(len=77) -> 0, 77` lines but no `query ALPN` line at all; `--trace-config socks --trace-config tcp`: `[SOCKS] query ALPN` and no `[TCP]` one. So only the topmost filter writes it.
- `--trace-config proxy --preproxy socks5h://127.0.0.1:18619 -x http://proxy.test:3128 http://example.test/a`: `[SOCKS] query ALPN` between `Established connection` and `using HTTP/1.x`.
- `https://example.test/a` through SOCKS5h, `--trace-config socks --trace-config ssl -k`: after `Opened SOCKS connection` come the `[SSL] cf_connect()` lines; the scripted proxy cannot finish the TLS handshake (exit 35), so the line after `Established` was not recorded.

Decisions (ADR-0357's BL-1246 amendment):
- Over TLS no `[SOCKS] query ALPN`: the TLS filter sits above the SOCKS filter and answers, and the `network` and `tcp` measurements show only the topmost filter writes the line.
- Written only for a target with `PoolScheme` `http` (the HTTP handler's connections); other protocols ask no ALPN, as BL-1195 measured for `[TCP]`.
- `IsThroughSocks` is the target's own SOCKS proxy or the pre-proxy of a forward proxy (`TunnelProxyOf`); through a CONNECT tunnel behind a pre-proxy the `[H1-PROXY]` filter is topmost, unchanged.
- ADR-0357 joined `touches` for the amendment; no task in Doing on `origin/work/dark-factory` names it.
- No `Curl.Console` production change was needed: `CurlComposition.TracesSocks` already maps `socks`, `proxy` and a named `all`.

Tests: `TcpConnectorTests.SocksFilterTrace` (the line after the opened event for SOCKS4, 4a, 5, 5h; only `[SOCKS]` under socks and tcp; none under tcp alone, for no or `ftp` scheme, or for https; through a pre-proxy to a forward proxy) and `CurlCommandRunnerSocksQueryAlpnTraceTests` (the line right after `Established connection` and before `using HTTP/1.x` under `socks` and `proxy`, once under `all`, none under `-v`, `-vvvv` and `network`). `dotnet build Curl.slnx -warnaserror` clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 0 failing members.

## Log

- 2026-10-02: Created.
- 2026-10-02: Renumbered from BL-1217, which the archived Done/2026-10-02_1625 task already holds.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. [SOCKS] query ALPN is written after Established connection for plain HTTP through a SOCKS proxy or pre-proxy under socks, proxy and a named all
