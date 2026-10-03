---
id: BL-1284
title: Write the [HTTPS-CONNECT] trace lines of an --http3 connect over QUIC
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1284 — Write the [HTTPS-CONNECT] trace lines of an --http3 connect over QUIC

## Goal

Curl writes curl's `[HTTPS-CONNECT]` lines (and the origin's `[SETUP]` lines) under `--trace-config https-connect`, `all` and `-vvvv` for an `--http3` or `--http3-only` transfer, as it does for a TCP connect.

## Context

- Split from BL-1254, which wrote them through proxies and over Unix sockets. QUIC connects go through `TcpConnector.ConnectMultiplexedAsync`; the filter is `HttpsConnectFilterTraceEvents` with `HttpsConnectFirstAttemptVersion` `h3`.
- The reference curl 8.21.0 (mingw, Schannel) has no HTTP/3, so BL-1254 could not measure it. Measure with an HTTP/3 build (curl.se's Windows build with ngtcp2, ADR-0180) against a QUIC server, and note how the h3 attempt's lines, any h2 fallback attempt (`2nd attempt`), and `[SETUP]` sit beside the `QUIC connect to` lines.

## Acceptance criteria

- [ ] Measured first, stderr in Notes, for `--http3` and `--http3-only` to a loopback QUIC server.
- [ ] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the measured lines; ADR-0357 gets an amendment.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
