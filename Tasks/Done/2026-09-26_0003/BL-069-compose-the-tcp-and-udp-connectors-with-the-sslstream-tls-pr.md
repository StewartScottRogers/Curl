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
completed: 2026-09-26
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

- [x] The composition root builds exactly one `SystemDnsResolver`, passed to both the
      `TcpConnector` and the `UdpDatagramConnector`, and passes `TimeProvider.System` to
      both.
- [x] The `TcpConnector` receives a `TcpDialer` and an `SslStreamTlsProvider` whose
      `TlsClientOptions` has `Insecure` equal to `false`.
- [x] Named tests in `Curl.Console.UnitTests` assert each of the two statements above.
- [x] Nothing in `Curl.Console` constructs a `Socket`, `SslStream` or `HttpClient`.
- [x] No test is tagged `Integration` and no test opens a socket.
- [x] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

- Plan (run in-session; a one-file composition change did not need the full agent pipeline): `CurlComposition.CreateTransports()` builds the resolver, `TimeProvider.System`, `TcpDialer`, `new TlsClientOptions()` and `SslStreamTlsProvider` once, then both connectors from those same instances, and returns them all in the internal record `CurlTransports`.
- Choice: the networking types keep their dependencies private, so the tests read the one private field of each dependency's type by reflection (`CapturedDependency<T>`) to prove the connectors received the very instances the record exposes. Reflection is allowed in test projects; matching on field type rather than name survives renames. Default taken because the alternative, public accessors on the networking types, is outside `touches`.
- Nothing calls `CreateTransports()` from the runner yet; BL-070 registers the network handlers over it, so each run builds it once there.
- Verified: `dotnet build Curl.Console -warnaserror` clean, `dotnet format --verify-no-changes` clean for both projects, Curl.Console.UnitTests 37/37, solution fast tests green.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. CurlComposition.CreateTransports builds the TCP connector (SystemDnsResolver, TcpDialer, secure SslStreamTlsProvider) and UDP connector over one resolver and TimeProvider.System
