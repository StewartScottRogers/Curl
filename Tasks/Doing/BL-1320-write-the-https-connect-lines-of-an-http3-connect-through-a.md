---
id: BL-1320
title: Write the [HTTPS-CONNECT] lines of an --http3 connect through a CONNECT-UDP proxy and of the Alt-Svc race that tries TCP first
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1320 — Write the [HTTPS-CONNECT] lines of an --http3 connect through a CONNECT-UDP proxy and of the Alt-Svc race that tries TCP first

## Goal

Curl writes curl's `[HTTPS-CONNECT]` and `[SETUP]` lines for an `--http3` connect through an HTTP or HTTPS proxy (CONNECT-UDP) and for the `--http3` race that tries TCP first for an `--alt-svc` entry naming h2 or h1.

## Context

Split from BL-1284, which wrote them for a direct QUIC connect (ADR-0357's BL-1284 amendment, `TcpConnector.QuicHttpsConnect.cs`). Measure with curl.se's curl 8.22.0 ngtcp2 build (`%TEMP%rl-8.22`) as BL-1284 Notes describe, with a loopback Kestrel HTTP/3 server; `TcpConnector.UdpTunnel.cs` and `HttpProtocolHandler.RaceTcpAgainstQuicAsync` are where to start.

## Acceptance criteria

- [ ] Measured first, stderr in Notes, for both cases.
- [ ] Tests in `Curl.Networking.UnitTests` (and `Curl.Protocol.Http.UnitTests` if the race changes) pin the measured lines; ADR-0357 gets an amendment.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
