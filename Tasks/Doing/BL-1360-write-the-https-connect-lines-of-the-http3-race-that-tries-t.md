---
id: BL-1360
title: Write the [HTTPS-CONNECT] lines of the --http3 race that tries TCP first for an --alt-svc entry naming h2 or h1
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1360 — Write the [HTTPS-CONNECT] lines of the --http3 race that tries TCP first for an --alt-svc entry naming h2 or h1

## Goal

Curl writes curl 8.22.0's `[HTTPS-CONNECT]` lines for the `--http3` race that tries TCP first because an `--alt-svc` entry names the origin itself with `h2` or `h1`.

## Context

Split from BL-1320, which wrote them for QUIC through a CONNECT-UDP proxy and measured this case too (BL-1320 Notes hold the stderr). The connector cannot tell this race from the QUIC-first one: `AltSvcTransferCache.ApplyTo` (Curl.Console) sets `HttpRequestOptions.TriesTcpBeforeQuic` but drops which ALPN the entry named, and `ConnectTarget` carries neither. Suggested shape: carry the entry's version (`h2`/`h1`) from `AltSvcTransferCache` through `HttpRequestOptions` to a `ConnectTarget` property set by `HttpProtocolHandler.RaceTcpAgainstQuicAsync`; `TcpConnector` then writes `1st attempt uses <h2|h1> from preferred version` and `2nd attempt uses h3 from wanted versions`, keeps the TCP attempt's state on the target as `TcpConnector.QuicHttpsConnect.cs` does the other way round, and the QUIC attempt continues the filter with `<h2|h1> baller failed, starting h3` (or `inconclusive after <ms>`). Measured with curl.se's curl 8.22.0 ngtcp2 build (`%TEMP%\curl-8.22`) and `Record-CurlExchange.ps1 -Tls [-UdpSink | -NoServer]` with an alt-svc file line `h2 127.0.0.1 <port> h2 127.0.0.1 <port> "20991231 00:00:00" 0 0`.

## Acceptance criteria

- [ ] Tests in `Curl.Networking.UnitTests` pin the measured lines for TCP connecting first (h2 and h1 entries) and for TCP refused with QUIC refused (`h2 baller failed, starting h3`, then `connect, all attempts failed` / `connect -> 7, done=0`).
- [ ] ADR-0357 gets an amendment.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Blocked. Stewart: dark factory timed out after 120 min; see Z:\repos\Curl.logs\BL-1360-20261003-061205-L7.jsonl
- 2026-10-03: Blocked -> Doing.
