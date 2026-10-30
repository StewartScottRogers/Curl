---
id: BL-4263
title: Extension: offer ALPN on TLS over a --npipe named pipe
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4206]
touches: [Curl.Protocol.Abstractions.UnitLibrary/ITlsProvider.cs, Curl.Protocol.Abstractions.UnitTests/ITlsProviderTests.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Extension.Abstractions.UnitLibrary/Certificates/CertificateCheckingTlsProvider.cs, Curl.Extension.Abstractions.UnitTests/Certificates, Curl.Extension.NamedPipe.UnitLibrary/NamedPipeConnector.cs, Curl.Extension.NamedPipe.UnitTests, Curl.Protocol.Abstractions.UnitLibrary/IConnector.cs, Curl.Protocol.Abstractions.UnitTests/IConnectorTests.cs, Curl.Networking.UnitLibrary/PoolingConnector.cs, Curl.Networking.UnitTests/PoolingConnectorTests.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.Alpn.cs, Curl.Networking.UnitTests/HandBuiltTlsProviderTests.cs, Curl.Extension.NamedPipe.UnitLibrary/NamedPipeConnectionHook.cs, Curl.Extension.NamedPipe.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4263 — Extension: offer ALPN on TLS over a --npipe named pipe

## Goal

`curl --npipe \.\pipe\name https://host/` offers curl's ALPN list (`h2`, `http/1.1`, or what `--http1.1`, `--http2` and `--no-alpn` leave) in its TLS handshake over the pipe, as `--unix-socket` does, so an `https://` transfer over a pipe can run HTTP/2.

## Context

BL-4206 (ADR-1055) runs curl's TLS handshake over the pipe through the public `ITlsProvider.AuthenticateAsClientAsync(plaintext, host, events, token)`, which offers no ALPN: the overload that does (`isProxy`, `applicationProtocols`) is on `Curl.Networking`'s internal `IHandshakeReportingTlsProvider`, which an extension cannot reach. Add a public default ALPN-offering overload to `Curl.Protocol.Abstractions`' `ITlsProvider` (default: the four-argument one), implement it in `SslStreamTlsProvider`, `HandBuiltTlsProvider` and `CertificateCheckingTlsProvider`, and have `NamedPipeConnector` pass `ConnectTarget.ApplicationProtocols` for an `https` pool scheme as `TcpConnector.ApplicationProtocolsFor` does.

## Acceptance criteria

- [x] An `https://` transfer under `--npipe` offers the target's ALPN protocols over the pipe; unit tests over a fake pipe and a fake TLS provider.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-29: Added a public ALPN-offering default overload to `ITlsProvider` (by default it calls the four-argument one and offers nothing) and `IConnector.HttpOverTlsApplicationProtocols` (default `http/1.1` alone, what curl offers on Windows with no version option; `TcpConnector` already had the property, and `PoolingConnector` now forwards it). `NamedPipeConnectionHook` passes the list from curl's connector, and `NamedPipeConnector` offers the target's own list, or else that one, for an `https` pool scheme, and nothing for other TLS schemes, as `TcpConnector.ApplicationProtocolsFor` does. The decision is in ADR-1066.
- 2026-10-29: `Measure-CodeQuality.ps1` over Curl.Protocol.Abstractions, Curl.Networking, Curl.Extension.Abstractions and Curl.Extension.NamedPipe found 0 failing members. Build clean and fast tests green. The tests use a fake pipe and a fake TLS provider and do not depend on the platform, so this also covers CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 6a527e98 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. curl --npipe https:// now offers curl's ALPN list in its TLS handshake over the pipe
