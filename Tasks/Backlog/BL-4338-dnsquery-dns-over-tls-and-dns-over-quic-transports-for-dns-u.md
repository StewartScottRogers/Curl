---
id: BL-4338
title: DnsQuery: DNS-over-TLS and DNS-over-QUIC transports for dns:// URLs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4337]
touches: [Curl.Extension.DnsQuery.UnitLibrary/Transport/DnsQueryTransportSelection.cs, Curl.Extension.DnsQuery.UnitLibrary/Transport/Tls, Curl.Extension.DnsQuery.UnitLibrary/Transport/Quic, Curl.Extension.DnsQuery.UnitLibrary/Curl.Extension.DnsQuery.UnitLibrary.csproj, Curl.Extension.DnsQuery.UnitLibrary/CLAUDE.md, Curl.Extension.DnsQuery.UnitTests/Transport/Tls, Curl.Extension.DnsQuery.UnitTests/Transport/Quic]
requirement: none
created: 2026-10-29
completed:
---
# BL-4338 — DnsQuery: DNS-over-TLS and DNS-over-QUIC transports for dns:// URLs

## Goal

`dns://` URLs can be queried over DNS-over-TLS (RFC 7858) and DNS-over-QUIC (RFC 9250), selected through BL-4337's transport selection.

## Context

Split from BL-4150 (extension 50 of 63, ADR-0999, CLAUDE.md "Extensions"). Tested against RFC 1035 and RFC 4501, not against curl; never changes curl's behaviour. Never reference a `Curl.Protocol.*` library or another extension (in particular not `Curl.Extension.EncryptedDns`, whose DoT/DoQ resolvers and wire codec are a model to follow but must be written here); `Curl.Networking.UnitLibrary` and `Curl.Quic.UnitLibrary` are shared layers it may reference, as EncryptedDns does.

Builds on BL-4337's `Transport/DnsQueryTransportSelection.cs`. DoT: `ProtocolHandlerServices.TlsProvider` over the connector, port 853, length-prefixed messages. DoQ: `Curl.Quic.UnitLibrary`'s client over the datagram connector with ALPN `doq`, Message ID 0 (RFC 9250 section 4.2.1), certificate verified through the TLS provider (see how EncryptedDns's `TlsProviderCertificateVerifier` does it, and write this extension's own). Tests use fake connections; DoQ may run the real client against an in-memory QUIC test server copied into the test project.

## Acceptance criteria

- [ ] DoT sends the query length-prefixed over TLS on port 853 by default and prints the answer; unit tests over a fake connection cite RFC 7858 sections.
- [ ] DoQ sends the query on a client bidirectional stream with ALPN `doq`, Message ID 0 and FIN, and prints the answer; unit tests cite RFC 9250 sections.
- [ ] The extension's ADR (from BL-4337) or a new one records the DoT and DoQ choices.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes



## Log

- 2026-10-29: Created.
