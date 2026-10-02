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
completed:
---
# BL-1160 — Write the proxy filter ([SOCKS], [HTTP-PROXY], [H1-PROXY], [HAPROXY]) and [HTTPS-CONNECT] trace lines for --trace-config

## Goal

Curl writes curl 8.21.0's `[SOCKS]`, `[HTTP-PROXY]`, `[H1-PROXY]`, `[HAPROXY]` and `[HTTPS-CONNECT]` lines under their `--trace-config` names, `network`/`proxy` where curl groups them, `all` and `-vvvv`.

## Context

- Split from BL-1103 (ADR-0357), which delivered `[SETUP]` around a direct connect only. `TcpConnector.TracingConnectionFilters` wraps only `ConnectDirectlyAsync`; the proxy paths (`ConnectThroughProxyAsync`, `--preproxy`, `--haproxy-protocol`) and the HTTPS ALPN connect filter are not traced yet, and the `[SETUP]` and `[DNS]` lines through a proxy are unmeasured.
- Measure each through `Record-CurlExchange.ps1` with a loopback proxy (extend the script if it cannot play one).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` for each component named in the title (a plain HTTP transfer, a refused connect, and `localhost` where both families answer); stderr in Notes.
- [ ] Tests pin each component's stable lines for a plain HTTP transfer, and that no line appears without its component; an ADR-0357 amendment records how the volatile values (fd numbers, nanosecond stamps, poll repetitions) are produced.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
