---
id: BL-4718
title: Pad DNS over HTTPS and DNS over QUIC queries with EDNS(0) Padding
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Https, Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Quic, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/Https, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/Quic]
requirement: none
created: 2026-11-03
completed:
---
# BL-4718 — Pad DNS over HTTPS and DNS over QUIC queries with EDNS(0) Padding

## Goal

`--dns-transport https` and `--dns-transport quic` queries carry the EDNS(0) Padding option (RFC 7830), padded to a multiple of 128 octets as RFC 8467 section 4.1 recommends, like DNS over TLS since ADR-1230.

## Context

BL-4689 added `Wire/DnsEdnsPadding.Pad` and calls it from `Transport/Tls/DnsTlsTransport` only (ADR-1230). Call it from `Transport/Https/DnsHttpsTransport` (before the POST body is written) and `Transport/Quic/DnsQuicTransport` (after the message ID is rewritten to 0), and update ADR-1230's DoH and DoQ bullet.

## Acceptance criteria

- [ ] A unit test in `Curl.Extended.Protocol.DnsQuery.UnitTests` shows a DoH query body and a DoQ stream payload each 128 octets long with ARCOUNT 1 and an OPT record whose first option is Padding (code 12).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-03: Created.
