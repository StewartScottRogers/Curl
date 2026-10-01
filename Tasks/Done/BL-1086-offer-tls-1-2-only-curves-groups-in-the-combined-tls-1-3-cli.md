---
id: BL-1086
title: Offer TLS 1.2-only --curves groups in the combined TLS 1.3 ClientHello's supported_groups as OpenSSL's curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1086 — Offer TLS 1.2-only --curves groups in the combined TLS 1.3 ClientHello's supported_groups as OpenSSL's curl does

## Goal

`--curves brainpoolP256r1:X25519` (TLS 1.3 offered) sends `supported_groups` [0x001a, 0x001d] with one X25519 key share, as Ubuntu's curl 8.18.0 with OpenSSL 3.5.5 does, instead of dropping brainpoolP256r1 from the list.

## Context

- Measured 2026-10-01 (BL-1082) with `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1`: `brainpoolP256r1:X25519` offers [0x001a, 0x001d]; `*brainpoolP256r1:*P-384` offers [0x001a, 0x0018] with key share [0x0018].
- `HandBuiltTlsProvider.ClientSettings.ToTls13` filters `SupportedGroups` by `TlsNamedGroup.CanShare`, and `Tls13ClientSettings` (Curl.Tls) requires every supported group to be shareable, so TLS 1.2-only groups vanish from the combined ClientHello.
- `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithCurves_OffersTheMeasuredGroupsAndKeyShares` pins `brainpoolP256r1:X25519` as [0x001d]; that row is wrong and must change to the measured value.
- The TLS 1.3 client must still accept a ServerHello/HelloRetryRequest only for a group it can share.

## Acceptance criteria

- [x] The `brainpoolP256r1:X25519` row pins [0x001a, 0x001d], and a `*brainpoolP256r1:*P-384` row pins [0x001a, 0x0018] with key share [0x0018].
- [x] A HelloRetryRequest naming a TLS 1.2-only group fails as today's unoffered-group case does.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member in Curl.Tls.UnitLibrary or Curl.Networking.UnitLibrary.

## Notes

- `Tls13ClientSettings` now accepts TLS 1.2-only ECDHE groups (`TlsNamedGroup.IsTls12EcdheGroup`) in `SupportedGroups`; key share groups must still pass `CanShare`, and a HelloRetryRequest naming a group `CanShare` refuses is an `illegal_parameter`, as an unoffered group is.
- `HandBuiltTlsProvider.ClientSettings.ToTls13(alongsideTls12)`: the combined TLS 1.3 + 1.2 ClientHello keeps the profile's groups as measured; a TLS 1.3-only ClientHello (`--tlsv1.3`) still drops TLS 1.2-only groups. Default taken (not measured here): OpenSSL's `tls_valid_group` rejects a group whose highest TLS version is below the connection's minimum.
- Build clean with -warnaserror, all fast tests green, Measure-CodeQuality.ps1: 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Combined TLS 1.3 ClientHello offers TLS 1.2-only --curves groups in supported_groups, as OpenSSL's curl does
