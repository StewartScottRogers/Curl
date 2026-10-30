---
id: BL-4338
title: DnsQuery: DNS-over-TLS and DNS-over-QUIC transports for dns:// URLs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4337]
touches: [Curl.Extension.DnsQuery.UnitLibrary/Transport/DnsQueryTransportSelection.cs, Curl.Extension.DnsQuery.UnitLibrary/Transport/Tls, Curl.Extension.DnsQuery.UnitLibrary/Transport/Quic, Curl.Extension.DnsQuery.UnitLibrary/Curl.Extension.DnsQuery.UnitLibrary.csproj, Curl.Extension.DnsQuery.UnitLibrary/CLAUDE.md, Curl.Extension.DnsQuery.UnitTests/Transport/Tls, Curl.Extension.DnsQuery.UnitTests/Transport/Quic, Curl.Extension.DnsQuery.UnitLibrary/Client/IDnsQueryTransport.cs, Curl.Extension.DnsQuery.UnitLibrary/Client/DnsQueryRequest.cs, Curl.Extension.DnsQuery.UnitLibrary/Client/DnsQueryProtocolHandler.cs, Curl.Extension.DnsQuery.UnitLibrary/Transport/Tcp/DnsTcpTransport.cs, Curl.Extension.DnsQuery.UnitLibrary/Transport/Udp/DnsUdpTransport.cs, Curl.Extension.DnsQuery.UnitTests/Transport/DnsQueryTransportSelectionTests.cs, Curl.Extension.DnsQuery.UnitTests/Client/DnsQueryProtocolHandlerTests.cs, Curl.Extension.DnsQuery.UnitTests/Client/DnsQueryRequestTests.cs, Curl.Extension.DnsQuery.UnitTests/Transport/DnsUdpTransportTests.cs, Curl.Quic.UnitLibrary/Curl.Quic.UnitLibrary.csproj]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4338 — DnsQuery: DNS-over-TLS and DNS-over-QUIC transports for dns:// URLs

## Goal

`dns://` URLs can be queried over DNS-over-TLS (RFC 7858) and DNS-over-QUIC (RFC 9250), selected through BL-4337's transport selection.

## Context

Split from BL-4150 (extension 50 of 63, ADR-0999, CLAUDE.md "Extensions"). Tested against RFC 1035 and RFC 4501, not against curl; never changes curl's behaviour. Never reference a `Curl.Protocol.*` library or another extension (in particular not `Curl.Extension.EncryptedDns`, whose DoT/DoQ resolvers and wire codec are a model to follow but must be written here); `Curl.Networking.UnitLibrary` and `Curl.Quic.UnitLibrary` are shared layers it may reference, as EncryptedDns does.

Builds on BL-4337's `Transport/DnsQueryTransportSelection.cs`. DoT: `ProtocolHandlerServices.TlsProvider` over the connector, port 853, length-prefixed messages. DoQ: `Curl.Quic.UnitLibrary`'s client over the datagram connector with ALPN `doq`, Message ID 0 (RFC 9250 section 4.2.1), certificate verified through the TLS provider (see how EncryptedDns's `TlsProviderCertificateVerifier` does it, and write this extension's own). Tests use fake connections; DoQ may run the real client against an in-memory QUIC test server copied into the test project.

## Acceptance criteria

- [x] DoT sends the query length-prefixed over TLS on port 853 by default and prints the answer; unit tests over a fake connection cite RFC 7858 sections.
- [x] DoQ sends the query on a client bidirectional stream with ALPN `doq`, Message ID 0 and FIN, and prints the answer; unit tests cite RFC 9250 sections.
- [x] The extension's ADR (from BL-4337) or a new one records the DoT and DoQ choices.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decisions (ADR-1141): the names are `tls` and `quic`, beside `udp` and `tcp`. Each transport now gives its own
  default port (`IDnsQueryTransport.DefaultPort`), so a URL naming no port is asked on 853 over TLS and QUIC, and a URL
  naming no server asks the system's first DNS server's address on that port. DoT reuses the TCP transport's framing
  (`DnsTcpTransport.ExchangeOverAsync`) rather than copying it. DoQ sends Message ID 0 and puts the query's ID back on
  the answer, so the handler checks every transport's answer the same way.
- Touches grew by the files the default port runs through (`IDnsQueryTransport.cs`, `DnsQueryRequest.cs`,
  `DnsQueryProtocolHandler.cs`, the UDP and TCP transports and their tests, the selection and request tests) and
  `Curl.Quic.UnitLibrary.csproj`, for one `InternalsVisibleTo` so the copied in-memory QUIC test server can use the
  client's internals as EncryptedDns's tests do. No task in Doing on `origin/work/dark-factory` named any of them.
- The QUIC test server, its TLS server, `DoqTestChannel` and `FakeDatagramConnector` are copied from
  `Curl.Extension.EncryptedDns.UnitTests/DnsOverQuic`; `QuicTestTlsServer` refers to `global::Curl.Tls.ClientHello`
  because the test namespace `...Transport.Tls` hides `Curl.Tls`.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.DnsQuery.UnitLibrary` reports 0 failing members;
  `dotnet build` clean; the fast tests pass (166 in `Curl.Extension.DnsQuery.UnitTests`). The tests are
  platform-neutral (no paths, OS error text or platform certificates), which is what CI on Linux and macOS needs.


## Log

- 2026-10-29: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 121af646 7614c36d 1fbc52b6 478f867b 3d836d5e.
- 2026-10-30: Doing -> Done. dns:// URLs are queried over DNS over TLS and DNS over QUIC with --dns-transport tls and quic, port 853 by default
