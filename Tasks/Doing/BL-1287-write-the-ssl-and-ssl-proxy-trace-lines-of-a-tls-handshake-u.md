---
id: BL-1287
title: Write the [SSL] and [SSL-PROXY] trace lines of a TLS handshake under --trace-config ssl and proxy
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1287 — Write the [SSL] and [SSL-PROXY] trace lines of a TLS handshake under --trace-config ssl and proxy

## Goal

Curl writes curl 8.21.0's `[SSL]` lines around an origin's TLS handshake and `[SSL-PROXY]` lines around an HTTPS proxy's, under `--trace-config ssl`, `proxy` (proxy only) and `all`, as the reference build does.

## Context

- Found in BL-1255 (ADR-0357's BL-1255 amendment). No `[SSL]` or `[SSL-PROXY]` line is written anywhere yet, and the console sets no flag for the `ssl` group.
- Measured 2026-10-02 (curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Tls -Script` playing an HTTPS proxy, `-s -v --trace-config proxy -p --proxy-insecure -x https://127.0.0.1:P http://example.test/x`): after `[HTTP-PROXY] CONNECT`, `[SSL-PROXY] cf_connect()`, the schannel lines and `ALPN: curl offers http/1.1`, `[SSL-PROXY] cf_connect() -> 0, done=0`, `[SSL-PROXY] adjust_pollset, POLLIN fd=N`, then each poll round `[HTTP-PROXY] CONNECT`, `[SSL-PROXY] cf_connect()`, and finally `ALPN: server did not agree on a protocol. Uses default.`, `[SSL-PROXY] cf_connect() -> 0, done=1`, `[SSL-PROXY] query ALPN`, `[SSL-PROXY] query ALPN: returning '(nil)'` before `CONNECT: no ALPN negotiated`. `fd=N` is volatile.
- Today Curl writes BL-1255's two handshake-poll `[HTTP-PROXY] CONNECT` lines after the handshake's `-v` lines (`HttpProxyTunnelTrace.ReportProxyHandshakePolled`); placing them among the handshake's lines belongs here, since the console renders those lines from the handshake event.
- Measure the origin's `[SSL]` lines for a direct `https://` connect first; OpenSSL build differences too.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (direct `https://`, and through `-x https://`, `--trace-config ssl`, `proxy` and `all`), stderr in Notes.
- [ ] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the `[SSL]` and `[SSL-PROXY]` lines and that none appears without its group; ADR-0357 gets an amendment for every volatile value.
- [ ] `--ai-help` still describes `--trace-config` correctly.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for the touched libraries.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
