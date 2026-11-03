---
id: BL-4672
title: Fix DnsQuery conformance: CurlSurface:Verbose-Dns-1
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Udp/DnsUdpTransport.cs, Curl.Extended.Protocol.DnsQuery.UnitLibrary/CLAUDE.md, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/DnsUdpTransportTests.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed:
---
# BL-4672 — Fix DnsQuery conformance: CurlSurface:Verbose-Dns-1

## Goal

`curl -v dns://localhost/example.com` writes the connection line every transfer's `-v` writes, `* Established connection to localhost (127.0.0.1 port 53) from ...`, on standard error before the answer.

## Context

Found by BL-4624 (`Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/DnsTransferSurfaceCases.cs`,
`Verbose_SuccessfulQuery_...`). Today `-v` on a successful dns query over UDP writes nothing at all to standard error.
curl 8.22.0 writes `* Established connection to localhost (127.0.0.1 port N) from ...` for every transfer (as the xmpp
and mqtt5 transfer-surface cases measured). `DnsUdpTransport.ExchangeAsync` opens its channel itself through
`IDatagramConnector.OpenAsync`, so nothing reports the connection. BL-4667 fixed the same gap for CoAP: its
`CoapProtocolHandler` calls `ReportConnectionOpened` (URL host, server address, the channel's local address or
`0.0.0.0:0`, the connector's connection number) after opening the UDP channel; do the same here.

## Acceptance criteria

- [ ] `CurlSurface:Verbose-Dns-1` passes in `DnsTransferSurfaceCases`, and its line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] A focused unit test in `Curl.Extended.Protocol.DnsQuery.UnitTests` pins the reported connection (ADR-0482).
- [ ] `Curl.Extended.Protocol.DnsQuery.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 7bcaef45 a09d4a4a 81704b3f 914c30cc.
