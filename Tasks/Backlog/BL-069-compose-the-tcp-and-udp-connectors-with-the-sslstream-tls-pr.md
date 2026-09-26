---
id: BL-069
title: Compose the TCP and UDP connectors with the SslStream TLS provider in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-068, BL-062]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-069 — Compose the TCP and UDP connectors with the SslStream TLS provider in Curl.Console

## Goal

The `Curl.Console` composition root builds the production transports once per run: a
`TcpConnector` over `SystemDnsResolver`, `TcpDialer` and `SslStreamTlsProvider`, and a
`UdpDatagramConnector`, sharing one resolver and `TimeProvider.System`, ready for the
network handlers BL-070 registers.

## Context

- BL-068 creates the composition root (plain constructor calls, no reflection, no
  dependency-injection package) and the runner. Extend that root; do not add a second.
- Types, all in `Curl.Networking.UnitLibrary` (ADR-0005): `TcpConnector(IDnsResolver,
  ITcpDialer, ITlsProvider, TimeProvider)`, `SystemDnsResolver()`, `TcpDialer`,
  `UdpDatagramConnector(IDnsResolver, TimeProvider)`, and `SslStreamTlsProvider(TlsClientOptions)`
  from BL-062.
- This task uses `new TlsClientOptions()`, which verifies certificates and leaves the TLS
  version to the operating system. BL-071 replaces it with options built from the
  command line.
- The composition exposes what it built through internal members so tests can check
  the graph without opening a socket.

## Acceptance criteria

- [ ] The composition root builds exactly one `SystemDnsResolver`, passed to both the
      `TcpConnector` and the `UdpDatagramConnector`, and passes `TimeProvider.System` to
      both.
- [ ] The `TcpConnector` receives a `TcpDialer` and an `SslStreamTlsProvider` whose
      `TlsClientOptions` has `Insecure` equal to `false`.
- [ ] Named tests in `Curl.Console.UnitTests` assert each of the two statements above.
- [ ] Nothing in `Curl.Console` constructs a `Socket`, `SslStream` or `HttpClient`.
- [ ] No test is tagged `Integration` and no test opens a socket.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
