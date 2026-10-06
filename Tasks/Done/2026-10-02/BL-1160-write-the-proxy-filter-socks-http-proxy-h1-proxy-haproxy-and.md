---
id: BL-1160
title: Write the proxy filter ([SOCKS], [HTTP-PROXY], [H1-PROXY], [HAPROXY]) and [HTTPS-CONNECT] trace lines for --trace-config
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1103]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1160 — Write the proxy filter ([SOCKS], [HTTP-PROXY], [H1-PROXY], [HAPROXY]) and [HTTPS-CONNECT] trace lines for --trace-config

## Goal

Curl writes curl 8.21.0's `[SOCKS]`, `[HTTP-PROXY]`, `[H1-PROXY]`, `[HAPROXY]` and `[HTTPS-CONNECT]` lines under their `--trace-config` names, `network`/`proxy` where curl groups them, `all` and `-vvvv`.

## Context

- Split from BL-1103 (ADR-0357), which delivered `[SETUP]` around a direct connect only. `TcpConnector.TracingConnectionFilters` wraps only `ConnectDirectlyAsync`; the proxy paths (`ConnectThroughProxyAsync`, `--preproxy`, `--haproxy-protocol`) and the HTTPS ALPN connect filter are not traced yet, and the `[SETUP]` and `[DNS]` lines through a proxy are unmeasured.
- Measure each through `Record-CurlExchange.ps1` with a loopback proxy (extend the script if it cannot play one).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` for each component named in the title (a plain HTTP transfer, a refused connect, and `localhost` where both families answer); stderr in Notes.
- [x] Tests pin each component's stable lines for a plain HTTP transfer, and that no line appears without its component; an ADR-0357 amendment records how the volatile values (fd numbers, nanosecond stamps, poll repetitions) are produced.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Split (as BL-1159 did, ADR-0357's BL-1160 amendment): this run delivers `[HAPROXY]` and pins `[SETUP]` through a plain HTTP proxy; `[HTTP-PROXY]`/`[H1-PROXY]` went to BL-1193, `[SOCKS]` to BL-1191, `[HTTPS-CONNECT]` to BL-1192 (filed with the board script, same `touches`). The acceptance boxes are ticked for the components this run delivers.
- Measured 2026-10-02 with `Record-CurlExchange.ps1`, curl 8.21.0 Schannel (non-trace lines trimmed):
  - `-s -v --trace-config all -x http://127.0.0.1:18460 http://example.test/x`: no proxy filter line at all; `[SETUP] happy eyeballing to origin 127.0.0.1:18460` names the proxy. `--trace-config proxy -v`: plain `-v` output only. `-vv -x http://localhost:P`: `[SETUP] added`, the resolve lines, `[SETUP] happy eyeballing to origin localhost:P`, `Trying [::1]:P...`, `Trying 127.0.0.1:P...`, ... - a direct connect's lines.
  - `-v --trace-config setup,haproxy --haproxy-protocol http://127.0.0.1:18475/x`:
    `[SETUP] added` / `[SETUP] happy eyeballing to origin 127.0.0.1:18475` / `  Trying 127.0.0.1:18475...` / `[SETUP] added HAPROXY filter` / `Established connection to ...` / `[SETUP] removing connected setup filter` / `[SETUP] destroy` / `[HAPROXY] removing connected setup filter` / `[HAPROXY] destroy` / `using HTTP/1.x` ...
  - `--trace-config haproxy -v` and `--trace-config proxy -v`: only the two `[HAPROXY]` lines after `Established connection`. `--trace-config network -v`: none. `-vv`: the `[SETUP]` lines with `added HAPROXY filter`, no `[HAPROXY]`. Under `all` the `[HAPROXY]` removal sits between `[SETUP] destroy` and `[HAPPY-EYEBALLS] removing ...`.
  - Refused (`--trace-config setup,haproxy -v --haproxy-protocol http://127.0.0.1:1/x`): `[SETUP] added`, `[SETUP] happy eyeballing to origin 127.0.0.1:1`, `Trying`, the connect failure lines; no `[HAPROXY]` line.
- No volatile values in these lines (ADR-0357 amendment says so).
- Over TLS the `[HAPROXY]` removal is written after `Established connection` too, unmeasured; BL-1192 checks it beside `[HTTPS-CONNECT]`.
- Tests: `TcpConnectorTests.HaproxyFilterTrace` (7) and `CurlCompositionDnsTraceTests` (+8 rows). `Measure-CodeQuality.ps1`: Curl.Networking.UnitLibrary and Curl.Console 100% line and branch, no failing member (the PROXY write moved into `WriteHaproxyLineAsync` to keep `SecureWhenAskedAsync` at complexity 10).

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config haproxy/proxy/all and -vvvv write curl's [HAPROXY] lines and [SETUP] added HAPROXY filter; [SETUP] through a plain HTTP proxy pinned; tunnel, SOCKS and HTTPS-CONNECT filed as BL-1193..BL-1192
