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
completed: 2026-10-02
---
# BL-1048 — Match OpenSSL's ClientHello under --curves: drop ec_point_formats without an EC group, add padding under 512 bytes, keep brainpool groups beside TLS 1.3

## Goal

With `--curves`, the hand-built client's ClientHello matches OpenSSL 3.5's curl byte for byte in three measured respects: `ec_point_formats` is left out when no EC group remains (`--curves X25519MLKEM768`), the `padding` extension (0x0015) is added when the hello falls between 256 and 511 bytes (`--curves X25519`, `--curves P-384:X25519`), and brainpool TLS 1.2 groups stay in `supported_groups` of a hello that also offers TLS 1.3 (`--curves brainpoolP256r1:X25519` sends 001a 001d).

## Context

- BL-709 / ADR-0284 Consequences. The captures are in BL-709's Notes (Ubuntu curl 8.18.0, OpenSSL 3.5.5): default order `ff01 0000 000b 000a 0010 0016 0017 0031 000d 002b 002d 0033 001b`; `X25519` adds `0015` last; `X25519MLKEM768` drops `000b`.
- `ClientHelloProfileMapping` and `HandBuiltTlsProvider.ClientSettings.ToTls13` shape the lists; the combined TLS 1.3 + 1.2 hello (`TlsClientConnection`) in `Curl.Tls.UnitLibrary` decides `supported_groups`. OpenSSL's padding rule is `ssl/statem/extensions_clnt.c` `tls_construct_ctos_padding`.

## Acceptance criteria

- [x] `HandBuiltTlsProviderTests` pin the extension order and `supported_groups` of the OpenSSL build's hello for each of the three values above as measured.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Decision: ADR-0350. `ClientHelloProfile.PadsTcpHello` (set on the OpenSSL profile) makes `HandBuiltTlsProvider.ClientSettings.ToTls13` put `padding` last in the order, after `early_data` and before `encrypted_client_hello`; `Tls13ClientHelloBuilder`'s existing 256-511 to 512 rule decides whether it is sent (measured `X25519` and `P-384:X25519` hellos land at 512 bytes). `ClientHelloProfileMapping.FixedExtensions` and `Tls12FixedExtensions` share one `EcPointFormats` rule: sent only while a TLS 1.2 ECDHE group remains.
- Brainpool beside TLS 1.3 already held (BL-1086); the new test pins it with the extension order.
- Defaults taken: a flag rather than `padding` in the profile's `ExtensionOrder`, so QUIC's hello (built from that order) is unchanged; the TLS 1.2-ceiling hello stays unpadded (BL-941 measured its order without padding). Both are unmeasured with a short hello: filed BL-1156.
- Test: `AuthenticateAsClientAsync_WithCurvesInTheOpenSslBuild_SendsTheMeasuredExtensionsAndGroups` (4 rows); `..._WithCurves_OffersTheMeasuredGroupsAndKeyShares` now compares the order without `ec_point_formats` and `padding`, which the new test pins.
- ADR-0350 and its index row added; an ADR needs no `touches` entry.
- Quality: `Curl.Tls.UnitLibrary` 100%/100%, 989 members, 0 failing; `Curl.Networking.UnitLibrary` 100%/100%, 1211 members, 0 failing (worst CRAP 10 each).
- Fast tests: all green except `Curl.Authentication.UnitTests`' `ForPlatform_Windows_IsTheSystemAnsiCodePage`, which failed once in the full run and passes alone; that project references neither changed library.
- `--ai-help` needs no change: no option was added or changed.

## Log

- 2026-09-30: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Under --curves the OpenSSL build's hand-built TLS 1.3 ClientHello pads a 256-511 byte hello to 512, drops ec_point_formats with no EC group left and keeps brainpool beside TLS 1.3, as measured
