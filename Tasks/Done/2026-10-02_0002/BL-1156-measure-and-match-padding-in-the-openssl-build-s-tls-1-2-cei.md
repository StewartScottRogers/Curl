---
id: BL-1156
title: Measure and match padding in the OpenSSL build's TLS 1.2-ceiling and QUIC ClientHellos under --curves
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1156 — Measure and match padding in the OpenSSL build's TLS 1.2-ceiling and QUIC ClientHellos under --curves

## Goal

The OpenSSL build's hand-built ClientHello under `--tls-max 1.2 --curves X25519` (TCP) and under `--http3-only --curves X25519` (QUIC) carries `padding` (0x0015) exactly when Ubuntu's curl 8.18.0 with OpenSSL 3.5.5 does, at the same place and length.

## Context

- BL-1048 / ADR-0350 pad the OpenSSL build's TLS 1.3 hello over TCP (`ClientHelloProfile.PadsTcpHello`, `HandBuiltTlsProvider.ClientSettings.WithPadding`), but leave the TLS 1.2-ceiling hello (`Tls12ClientHelloBuilder`) and the QUIC hello (`QuicClientSettings.CreateOpenSslTlsSettings`) unpadded because neither was measured with a hello of 256 to 511 bytes.
- OpenSSL's `tls_construct_ctos_padding` (`ssl/statem/extensions_clnt.c`) pads whenever `SSL_OP_TLSEXT_PADDING` is set, which `SSL_OP_ALL` includes; whether QUIC's hello is padded in OpenSSL 3.5 is to be measured.
- Measure with `Record-CurlExchange.ps1` in plain TCP mode through WSL as BL-709's Notes describe; QUIC needs a UDP capture (extend the script if needed).

## Acceptance criteria

- [x] `HandBuiltTlsProviderTests` pins the OpenSSL build's `--tls-max 1.2 --curves X25519` hello's extension order as measured.
- [x] `Curl.Quic.UnitTests` or `Curl.Networking.UnitTests` pins whether the OpenSSL build's QUIC hello under `--curves X25519` carries `padding`, as measured.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-10-02 (WSL, OpenSSL 3.5.5). TLS 1.2: `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1`, Ubuntu curl 8.18.0 `-sk --tls-max 1.2 --curves X25519`. To the IP literal: 192 bytes, `ff01 000b 000a 0010 0016 0017 000d`, no padding. To a 75-character host (`--resolve`): 512 bytes, `ff01 0000 000b 000a 0010 0016 0017 000d 0015`, padding last.
- QUIC: Ubuntu's curl has no HTTP/3 (`--http3-only` is exit 2), so OpenSSL 3.5.5's own QUIC client stood in: `openssl s_client -quic -alpn h3 -bugs -groups X25519 -servername <75 chars> -msg` (`-bugs` is `SSL_OP_ALL`, which curl sets). 335-byte hello, no padding; the same command over TCP pads to 512. No UDP capture or script extension was needed: `-msg` prints the hello before protection.
- Decision: ADR-0365. `Tls12ClientSettings.PadHello` (set from `ClientHelloProfile.PadsTcpHello`) appends `padding` by the rule moved to `PaddingExtension.DataLengthFor`; the TLS 1.2 half of a combined hello uses `Tls12ClientHelloBuilder.BuildUnpadded`, so it never brings a second padding. QUIC unchanged, now pinned.
- Tests: `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithCurvesUnderATls12CeilingInTheOpenSslBuild_SendsTheMeasuredExtensions` (2 rows), `..._WithCurvesAndALongHostInTheOpenSslBuild_SendsOnePadding`, `Tls12ClientHandshakeTests.PadHelloPadsOnlyAHelloOf256To511Bytes` (4 rows), `QuicClientSettingsTests.CreateOpenSslTlsSettings_WithX25519AndAHelloOf256To511Bytes_SendsNoPadding`.
- Quality: `Curl.Tls.UnitLibrary` 100%/100%, 990 members, 0 failing; `Curl.Networking.UnitLibrary` 100%/100%, 1314 members, 0 failing (worst CRAP 10 each). `Curl.Quic.UnitLibrary` unchanged (test only).
- `--ai-help` needs no change: no option was added or changed.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. OpenSSL build pads its TLS 1.2-ceiling hello to 512 as measured; its QUIC hello is pinned unpadded
