---
id: BL-4263
title: Extension: offer ALPN on TLS over a --npipe named pipe
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4206]
touches: [Curl.Protocol.Abstractions.UnitLibrary/ITlsProvider.cs, Curl.Protocol.Abstractions.UnitTests/ITlsProviderTests.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Extension.Abstractions.UnitLibrary/Certificates/CertificateCheckingTlsProvider.cs, Curl.Extension.Abstractions.UnitTests/Certificates, Curl.Extension.NamedPipe.UnitLibrary/NamedPipeConnector.cs, Curl.Extension.NamedPipe.UnitTests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4263 — Extension: offer ALPN on TLS over a --npipe named pipe

## Goal

`curl --npipe \.\pipe\name https://host/` offers curl's ALPN list (`h2`, `http/1.1`, or what `--http1.1`, `--http2` and `--no-alpn` leave) in its TLS handshake over the pipe, as `--unix-socket` does, so an `https://` transfer over a pipe can run HTTP/2.

## Context

BL-4206 (ADR-1049) runs curl's TLS handshake over the pipe through the public `ITlsProvider.AuthenticateAsClientAsync(plaintext, host, events, token)`, which offers no ALPN: the overload that does (`isProxy`, `applicationProtocols`) is on `Curl.Networking`'s internal `IHandshakeReportingTlsProvider`, which an extension cannot reach. Add a public default ALPN-offering overload to `Curl.Protocol.Abstractions`' `ITlsProvider` (default: the four-argument one), implement it in `SslStreamTlsProvider`, `HandBuiltTlsProvider` and `CertificateCheckingTlsProvider`, and have `NamedPipeConnector` pass `ConnectTarget.ApplicationProtocols` for an `https` pool scheme as `TcpConnector.ApplicationProtocolsFor` does.

## Acceptance criteria

- [ ] An `https://` transfer under `--npipe` offers the target's ALPN protocols over the pipe; unit tests over a fake pipe and a fake TLS provider.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
