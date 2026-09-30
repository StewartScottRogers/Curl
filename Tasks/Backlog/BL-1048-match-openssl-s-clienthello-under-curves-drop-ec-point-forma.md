---
id: BL-1048
title: Match OpenSSL's ClientHello under --curves: drop ec_point_formats without an EC group, add padding under 512 bytes, keep brainpool groups beside TLS 1.3
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1048 — Match OpenSSL's ClientHello under --curves: drop ec_point_formats without an EC group, add padding under 512 bytes, keep brainpool groups beside TLS 1.3

## Goal

With `--curves`, the hand-built client's ClientHello matches OpenSSL 3.5's curl byte for byte in three measured respects: `ec_point_formats` is left out when no EC group remains (`--curves X25519MLKEM768`), the `padding` extension (0x0015) is added when the hello falls between 256 and 511 bytes (`--curves X25519`, `--curves P-384:X25519`), and brainpool TLS 1.2 groups stay in `supported_groups` of a hello that also offers TLS 1.3 (`--curves brainpoolP256r1:X25519` sends 001a 001d).

## Context

- BL-709 / ADR-0284 Consequences. The captures are in BL-709's Notes (Ubuntu curl 8.18.0, OpenSSL 3.5.5): default order `ff01 0000 000b 000a 0010 0016 0017 0031 000d 002b 002d 0033 001b`; `X25519` adds `0015` last; `X25519MLKEM768` drops `000b`.
- `ClientHelloProfileMapping` and `HandBuiltTlsProvider.ClientSettings.ToTls13` shape the lists; the combined TLS 1.3 + 1.2 hello (`TlsClientConnection`) in `Curl.Tls.UnitLibrary` decides `supported_groups`. OpenSSL's padding rule is `ssl/statem/extensions_clnt.c` `tls_construct_ctos_padding`.

## Acceptance criteria

- [ ] `HandBuiltTlsProviderTests` pin the extension order and `supported_groups` of the OpenSSL build's hello for each of the three values above as measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-30: Created.
