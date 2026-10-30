---
id: BL-4337
title: DnsQuery: dns:// scheme over UDP and TCP (RFC 4501), registered
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4336]
touches: [Curl.Extension.DnsQuery.UnitLibrary/Client, Curl.Extension.DnsQuery.UnitLibrary/Transport/DnsQueryTransportSelection.cs, Curl.Extension.DnsQuery.UnitLibrary/Transport/Udp, Curl.Extension.DnsQuery.UnitLibrary/Transport/Tcp, Curl.Extension.DnsQuery.UnitLibrary/CLAUDE.md, Curl.Extension.DnsQuery.UnitTests/Client, Curl.Extension.DnsQuery.UnitTests/Transport, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.DnsQuery.UnitLibrary/Curl.Extension.DnsQuery.UnitLibrary.csproj, Curl.Extension.DnsQuery.UnitTests/Curl.Extension.DnsQuery.UnitTests.csproj]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4337 — DnsQuery: dns:// scheme over UDP and TCP (RFC 4501), registered

## Goal

`curl dns://[server]/name?type=A` sends an RFC 1035 query over UDP (falling back to TCP on a truncated answer) or over TCP, and prints the answer as text or JSON, as the registered `dns` extension.

## Context

Split from BL-4150 (extension 50 of 63, ADR-0999, CLAUDE.md "Extensions"). Tested against RFC 1035 and RFC 4501, not against curl; never changes curl's behaviour. Never reference a `Curl.Protocol.*` library or another extension (in particular not `Curl.Extension.EncryptedDns`, whose DoT/DoQ resolvers and wire codec are a model to follow but must be written here); `Curl.Networking.UnitLibrary` and `Curl.Quic.UnitLibrary` are shared layers it may reference, as EncryptedDns does.

Builds on BL-4336's `Wire/` and `Rendering/`. Parse the URL as RFC 4501 section 2 lays it out (`dnsauthority` optional, port 53 by default; `dnsname` percent-decoded; `;type=` and `;class=` and the `?type=` query form the BL-4150 goal names); with no server use the system resolver's first name server, or decide otherwise in the ADR. Register the handler by one line in `ExtensionCatalogue.cs` and its description by one line in `ExtensionDescriptionCatalogue.cs`, as `Curl.Extension.Coap`'s `CoapExtension` does (`ProtocolHandlerServices` gives the connector, datagram connector, TLS provider and resolver). Decide how a transport and the output format are chosen (an extension option such as `--dns-transport udp|tcp|tls|quic|https` and `--dns-json`, added to `ExtensionOptionCatalogue.cs`) in `Transport/DnsQueryTransportSelection.cs`, so BL-4338 and BL-4339 each add their transport there. Exit codes: decide and record (e.g. 6 for no answer from the server, 28 for timeout via injected `TimeProvider`).

## Acceptance criteria

- [x] The handler is registered by one line in `ExtensionCatalogue.cs` and its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] `dns://[server]/name?type=A` (and RFC 4501's `;type=` and `;class=`) queries over UDP and over TCP (two-octet length prefix, RFC 1035 section 4.2.2), retrying over TCP when the UDP answer has TC set (section 4.2.1), and prints text or JSON; unit tests over fake connections cite the RFC 1035 and RFC 4501 sections they check.
- [x] With `CURL_EXTENSIONS=0` a `dns://` URL fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the scheme and option names, the specification versions followed, the transport and format selection, and anything left out with why.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Touches widened (no Doing task on origin/work/dark-factory names any of them): `ExtensionTransferHookCatalogue.cs`,
  because extension handlers see only the transfer context and `--dns-transport`/`--dns-json` reach the handler
  through a transfer hook, as `--coap-observe` does; the library's csproj, for the `Curl.Networking` reference
  (`SystemDnsServers`, the system's first DNS server for a URL with no server); the test csproj, for the Composition
  and Core references the registration and `CURL_EXTENSIONS=0` tests need.
- Decisions in ADR-1126: options `--dns-transport udp|tcp` (default udp, exit 2 for another name) and `--dns-json`;
  `?type=`/`?class=` separated by `;` (RFC 4501) or `&`; UDP 3 tries of 5 s (dig's defaults) then exit 28, TCP retry
  when TC is set; exit 8 for an undecodable answer or a wrong message ID, 56 for a TCP close mid-answer; any RCODE
  is written with exit 0, as dig does. EDNS(0), DNSSEC and AXFR left out.
- Measured with the built curl.exe: `dns://9.9.9.9/example.com?type=A` answers over UDP and
  `--dns-transport tcp --dns-json dns://9.9.9.9/example.com?type=MX` over TCP, both exit 0; `curl --extensions` lists
  `dns`; `CURL_EXTENSIONS=0` gives exit 1 `curl: (1) Protocol "dns" not supported`; `dns:///example.com` is parsed by
  the command line as host `example.com` with no name, so exit 3.
- Measure-CodeQuality.ps1 -Library Curl.Extension.DnsQuery.UnitLibrary,Curl.Extension.Composition.UnitLibrary:
  0 failing members. `dotnet build` clean; fast tests green (Curl.Extension.DnsQuery.UnitTests 141). Platform-neutral:
  fake connections only, no paths or OS text, so CI on Linux and macOS is expected to match.



## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 8489097a 699d1913 588e50c5 8439c2d0 478f867b 3d836d5e.
- 2026-10-30: Doing -> Done. dns:// URLs query a DNS server over UDP (TCP retry on TC) or TCP and print text or JSON, registered as the dns extension
