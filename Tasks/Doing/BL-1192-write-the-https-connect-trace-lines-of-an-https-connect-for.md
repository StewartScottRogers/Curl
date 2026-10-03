---
id: BL-1192
title: Write the [HTTPS-CONNECT] trace lines of an https:// connect for --trace-config
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1192 — Write the [HTTPS-CONNECT] trace lines of an https:// connect for --trace-config

## Goal

Curl writes curl 8.21.0's `[HTTPS-CONNECT]` lines (the ALPN connect filter of an https:// transfer) under `--trace-config https-connect`, `all` and `-vvvv`.

## Context

- Split from BL-1160 (ADR-0357's BL-1160 amendment), which delivered the [HAPROXY] lines and pinned [SETUP] through a plain HTTP proxy. The TLS route is `TcpConnector.SecureWhenAskedAsync` and `AuthenticateTargetAsync`; `Record-CurlExchange.ps1 -Tls` plays the server. Check how the lines sit beside `[SETUP]` and a `--haproxy-protocol` `[HAPROXY]` line over TLS (BL-1160 put the [HAPROXY] removal after `Established connection` there unmeasured).
- Measure first with `Record-CurlExchange.ps1`; extend it to play the proxy if it cannot (it serves one HTTP exchange, so a plain `-x` proxy works already). Each line's place relative to `[SETUP]`, `[DNS]`, `Established connection` and the CONNECT lines matters; [HAPPY-EYEBALLS], [TCP], [MULTI] and [TIMER] are other tasks.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (a successful transfer and a refused connect), stderr in Notes; which `--trace-config` groups (`proxy`, `network`, `all`) turn the lines on is measured too.
- [ ] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the stable lines and that no line appears without its component; ADR-0357 gets an amendment for any volatile value.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
