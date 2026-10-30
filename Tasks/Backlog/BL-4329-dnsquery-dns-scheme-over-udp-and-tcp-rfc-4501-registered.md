---
id: BL-4329
title: DnsQuery: dns:// scheme over UDP and TCP (RFC 4501), registered
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4328]
touches: [Curl.Extension.DnsQuery.UnitLibrary/Client, Curl.Extension.DnsQuery.UnitLibrary/Transport/DnsQueryTransportSelection.cs, Curl.Extension.DnsQuery.UnitLibrary/Transport/Udp, Curl.Extension.DnsQuery.UnitLibrary/Transport/Tcp, Curl.Extension.DnsQuery.UnitLibrary/CLAUDE.md, Curl.Extension.DnsQuery.UnitTests/Client, Curl.Extension.DnsQuery.UnitTests/Transport, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4329 — DnsQuery: dns:// scheme over UDP and TCP (RFC 4501), registered

## Goal

`curl dns://[server]/name?type=A` sends an RFC 1035 query over UDP (falling back to TCP on a truncated answer) or over TCP, and prints the answer as text or JSON, as the registered `dns` extension.

## Context

Split from BL-4150 (extension 50 of 63, ADR-0999, CLAUDE.md "Extensions"). Tested against RFC 1035 and RFC 4501, not against curl; never changes curl's behaviour. Never reference a `Curl.Protocol.*` library or another extension (in particular not `Curl.Extension.EncryptedDns`, whose DoT/DoQ resolvers and wire codec are a model to follow but must be written here); `Curl.Networking.UnitLibrary` and `Curl.Quic.UnitLibrary` are shared layers it may reference, as EncryptedDns does.

Builds on BL-4328's `Wire/` and `Rendering/`. Parse the URL as RFC 4501 section 2 lays it out (`dnsauthority` optional, port 53 by default; `dnsname` percent-decoded; `;type=` and `;class=` and the `?type=` query form the BL-4150 goal names); with no server use the system resolver's first name server, or decide otherwise in the ADR. Register the handler by one line in `ExtensionCatalogue.cs` and its description by one line in `ExtensionDescriptionCatalogue.cs`, as `Curl.Extension.Coap`'s `CoapExtension` does (`ProtocolHandlerServices` gives the connector, datagram connector, TLS provider and resolver). Decide how a transport and the output format are chosen (an extension option such as `--dns-transport udp|tcp|tls|quic|https` and `--dns-json`, added to `ExtensionOptionCatalogue.cs`) in `Transport/DnsQueryTransportSelection.cs`, so BL-4330 and BL-4331 each add their transport there. Exit codes: decide and record (e.g. 6 for no answer from the server, 28 for timeout via injected `TimeProvider`).

## Acceptance criteria

- [ ] The handler is registered by one line in `ExtensionCatalogue.cs` and its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] `dns://[server]/name?type=A` (and RFC 4501's `;type=` and `;class=`) queries over UDP and over TCP (two-octet length prefix, RFC 1035 section 4.2.2), retrying over TCP when the UDP answer has TC set (section 4.2.1), and prints text or JSON; unit tests over fake connections cite the RFC 1035 and RFC 4501 sections they check.
- [ ] With `CURL_EXTENSIONS=0` a `dns://` URL fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the scheme and option names, the specification versions followed, the transport and format selection, and anything left out with why.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes



## Log

- 2026-10-29: Created.
