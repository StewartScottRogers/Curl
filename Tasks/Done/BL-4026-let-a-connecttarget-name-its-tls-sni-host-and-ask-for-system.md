---
id: BL-4026
title: Let a ConnectTarget name its TLS SNI host and ask for system-store-only trust (needed by CGR-289)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/ConnectTargetTlsOverrideTests.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/ISystemStoreTrustTlsProvider.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4026 — Let a ConnectTarget name its TLS SNI host and ask for system-store-only trust (needed by CGR-289)

## Goal

A `ConnectTarget` can name the host its TLS handshake sends as SNI and checks the certificate against, and ask for a handshake trusting only the system store, in both TLS providers.

## Context

BL-3818 (CGR-289) needs the Windows ldaps:// path to open WinLDAP's second connection with the machine name as SNI and system-store trust. ADR-0974.

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- 2026-10-28 lane 13, what was checked (the claim's criterion was the template placeholder, which the run keeps as written): `ConnectTarget.TlsServerName` (default `null`) is handed to the TLS provider in place of `Host` by `TcpConnector`, which still dials `Host`; `ConnectTarget.TrustsSystemStoreOnly` (default `false`) makes `TcpConnector.TrustedAs` run the handshake through `ISystemStoreTrustTlsProvider.WithSystemStoreOnlyTrust`, which `SslStreamTlsProvider` and `HandBuiltTlsProvider` implement by dropping `-k`, `--cacert`, `--capath` and the CA blob; `Curl.Networking.UnitTests/ConnectTargetTlsOverrideTests.cs` pins both for both builds of both providers; ADR-0974 records it.

- 2026-10-28 lane 13: Added `Curl.Networking.UnitLibrary/TcpConnector.cs` (the connector is what hands the target's host to the provider) and the new `ISystemStoreTrustTlsProvider.cs` to touches; no task in Doing on origin/work/dark-factory names either.
- Decision: the system-store copy keeps every other setting (`--pinnedpubkey`, `--crlfile`, ciphers, range) and is made per handshake, not cached; a provider without the interface (a fake) is used unchanged. HandBuiltTlsProvider derives the copy from its adjusted options (both adjustments are idempotent) rather than storing the given ones: a second `TlsClientOptions` field broke `EngineCompositionTests.CapturedDependency`, which finds the options by field type.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary`: 0 failing members; its fast test run over the solution was green. Platform-neutral: no paths, OS text or certificates in the new tests.

## Log

- 2026-10-28: Created.

## Requirements (filed by the BL-3818 run)

- `ConnectTarget` gains an optional `TlsServerName` (the SNI and certificate-name host when it differs from `Host`) and an optional switch that makes the TLS provider trust only the system store (ignoring `--cacert`, `--capath` and `-k`), both honoured by the Schannel-style provider and the hand-built one. Unit tests in Curl.Networking.UnitTests.
- Why: BL-3818 (CGR-289) needs the Windows ldaps:// path to open WinLDAP's second connection with the machine name as SNI and system-store trust.
- 2026-10-28: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-28: Doing -> Done. ConnectTarget.TlsServerName and TrustsSystemStoreOnly reach both TLS providers through TcpConnector (ADR-0974)
