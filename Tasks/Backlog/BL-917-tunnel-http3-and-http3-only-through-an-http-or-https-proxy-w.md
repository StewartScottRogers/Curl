---
id: BL-917
title: Tunnel --http3 and --http3-only through an HTTP or HTTPS proxy with CONNECT-UDP as curl 8.21.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-837]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-917 — Tunnel --http3 and --http3-only through an HTTP or HTTPS proxy with CONNECT-UDP as curl 8.21.0 does

## Goal

An `https://` transfer with `--http3` or `--http3-only` and `-x http://…` or `-x https://…` opens a CONNECT-UDP tunnel (RFC 9298) over the proxy connection and runs QUIC inside it through the capsule protocol (RFC 9297), as curl 8.21.0 does, instead of refusing HTTP/3 with `HTTP/3 is not supported over an HTTP proxy` as the 8.18.0 build does (BL-837, ADR-0222).

## Context

- BL-837 measured curl.se's 8.18.0 build and pinned its behaviour for HTTP proxies: `--http3-only` fails with exit 3 before connecting, `--http3` connects over TCP with the refusal as its error text. ADR-0187 makes 8.21.0 the HTTP/3 reference, and 8.21.0 no longer refuses an HTTP proxy: `Curl_conn_may_http3` (`lib/vquic/vquic.c`, tag `curl-8_21_0`) only refuses SOCKS.
- In 8.21.0, `lib/cf-setup.c` `cf_setup_add_origin_filters` puts a capsule filter (`Curl_cf_capsule_insert_after`) and a QUIC filter on top of the HTTP proxy tunnel filter when the transport is QUIC; `lib/http_proxy.c` then opens the tunnel as `CONNECT-UDP` (`udp_tunnel`), over HTTP/1.1 through `Curl_cf_h1_proxy_insert_after(..., udp_tunnel)` (or HTTP/2 when the HTTPS proxy negotiates h2). `USE_PROXY_HTTP3` (an HTTP/3 proxy, `--proxy-http3`-like) is off by default and out of scope.
- Start by measuring: no 8.21.0 ngtcp2 build was installed on 2026-09-29 (only curl.se's 8.18.0 under WinGet). Install curl.se's current Windows build (8.22.0 or later has the same code path) and record the CONNECT-UDP request bytes with `Record-CurlExchange.ps1` as the proxy (extend it to answer the upgrade if needed; add it to `touches`).
- The TCP race for `--http3` goes through the proxy with an ordinary `CONNECT` as today.

## Acceptance criteria

- [ ] The measured CONNECT-UDP request (bytes, curl version line) and curl's stderr for a refused CONNECT-UDP (e.g. the proxy answering 403) are copied into Notes for `--http3` and `--http3-only`.
- [ ] Tests in `Curl.Protocol.Http.UnitTests` pin: `--http3-only` through an HTTP proxy sends the measured CONNECT-UDP request and runs HTTP/3 over the tunnel; a refused tunnel fails with the measured exit code and message; `--http3` races it against a TCP `CONNECT` as measured.
- [ ] `HttpProtocolHandlerTests.Http3Proxy.cs`'s HTTP proxy cases are changed to the 8.21.0 behaviour, and ADR-0222's interim section is marked superseded.
- [ ] `curl --ai-help` needs no change, or is changed and says so.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each touched library.

## Notes

## Log

- 2026-09-29: Created.
