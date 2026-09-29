---
id: BL-896
title: Route --ciphers DHE-DSS names to the hand-built TLS 1.2 client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-802]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-896 — Route --ciphers DHE-DSS names to the hand-built TLS 1.2 client

## Goal

`curl --ciphers DHE-DSS-AES128-GCM-SHA256` (and every other DHE-DSS name) against a DSA server completes a TLS 1.2, 1.1 or 1.0 handshake through the hand-built client, as the OpenSSL build of curl does.

## Context

- BL-802 (ADR-0211) taught `Curl.Tls`'s `Tls12ClientHandshake` the 13 `DHE_DSS` suites and the `dsa_*` ServerKeyExchange signatures, but only when the caller sets `Tls12ClientSettings.CipherSuites` and `SignatureAlgorithms`.
- `Curl.Networking.UnitLibrary/OpenSslCipherSuites.cs` (`Tls12Names`) has no `DHE-DSS-*` names, so `--ciphers` cannot name them.
- `HandBuiltTlsProvider.ToTls12` (`Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs`) passes no `SignatureAlgorithms`, and the default list has no `dsa_*`, so a TLS 1.2 DSS ServerKeyExchange is refused with `illegal_parameter`. OpenSSL 3.5.5's measured ClientHello (ADR-0140) ends its `signature_algorithms` with `0402 0502 0602` (DSA); offer those (and `0202`, `0302` where the OpenSSL build does) when a DSS suite is offered, or take the list from `ClientHelloProfile.OpenSsl`. `--sigalgs` itself is BL-709's.
- OpenSSL names (`openssl ciphers -V`): `DHE-DSS-AES128-SHA` 0x0032, `DHE-DSS-AES256-SHA` 0x0038, `DHE-DSS-AES128-SHA256` 0x0040, `DHE-DSS-AES256-SHA256` 0x006a, `DHE-DSS-AES128-GCM-SHA256` 0x00a2, `DHE-DSS-AES256-GCM-SHA384` 0x00a3, `DHE-DSS-CAMELLIA128-SHA` 0x0044, `DHE-DSS-CAMELLIA256-SHA` 0x0087, `DHE-DSS-CAMELLIA128-SHA256` 0x00bd, `DHE-DSS-CAMELLIA256-SHA256` 0x00c3, `DHE-DSS-ARIA128-GCM-SHA256` 0xc056, `DHE-DSS-ARIA256-GCM-SHA384` 0xc057, `DHE-DSS-DES-CBC3-SHA` 0x0013. Verify each against a real OpenSSL build before pinning.

## Acceptance criteria

- [ ] `OpenSslCipherSuites` maps the 13 DHE-DSS names above to their code points, each pinned by a test.
- [ ] A hand-built TLS 1.2 connection offered a DHE-DSS suite also offers the `dsa_*` signature schemes, pinned by a test on the `Tls12ClientSettings` the provider builds.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed from BL-802's review (2026-09-29).

## Log

- 2026-09-29: Created.
