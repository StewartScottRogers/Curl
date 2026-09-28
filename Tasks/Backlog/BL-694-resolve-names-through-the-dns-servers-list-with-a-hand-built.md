---
id: BL-694
title: Resolve names through the --dns-servers list with a hand-built DNS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-643, BL-640]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-694 — Resolve names through the --dns-servers list with a hand-built DNS client

## Goal

With `--dns-servers <list>`, names are resolved by a hand-built DNS client in `Curl.Networking.UnitLibrary` that sends A and AAAA queries over UDP (retrying over TCP on truncation) to those servers, binding its socket per `--dns-interface`, `--dns-ipv4-addr` and `--dns-ipv6-addr`, as curl's c-ares build does, on every platform.

## Context

- Conformance audit 2026-09-28, row 28; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): the c-ares options are supported on every platform because curl's c-ares builds support them. Parsing: BL-643. Message codec: BL-640 (reuse it; do not write a second one).
- curl documents `--dns-servers` as "Set the list of DNS servers to be used instead of the system default" (https://curl.se/docs/manpage.html, checked 2026-09-28); the port syntax (`host:port`, `[v6]:port`) and the order servers are tried follow c-ares; record them from the manual and a measurement with a c-ares build of curl in Notes.
- The UDP socket sits behind a thin datagram seam like `ITcpDialer` (ADR-0083), so tests need no network; the resolver implements `IDnsResolver` and is composed in `Curl.Console/CurlTransports.cs` when the option is given. Failures map to exit 6 with curl's message.
- This client is also the SRV lookup BL-689 needs for Kerberos KDC location; expose SRV queries on it.

## Acceptance criteria

- [ ] Measured first with a c-ares build of curl through `Record-CurlExchange.ps1` extended as needed (a loopback DNS responder): the query bytes, the server order, the truncation fallback, a dead server; copied into Notes.
- [ ] `Curl.Networking.UnitTests` through the datagram seam pin the query bytes, server order and timeouts on a fake `TimeProvider`, the TCP retry on truncation, the bind for each of the three binding options, SRV answers, and exit 6 with the measured message.
- [ ] A `Curl.Console.UnitTests` test shows `--dns-servers` replacing the system resolver for a transfer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
