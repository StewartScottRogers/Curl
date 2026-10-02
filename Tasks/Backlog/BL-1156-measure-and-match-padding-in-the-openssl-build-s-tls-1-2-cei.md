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
completed:
---
# BL-1156 — Measure and match padding in the OpenSSL build's TLS 1.2-ceiling and QUIC ClientHellos under --curves

## Goal

The OpenSSL build's hand-built ClientHello under `--tls-max 1.2 --curves X25519` (TCP) and under `--http3-only --curves X25519` (QUIC) carries `padding` (0x0015) exactly when Ubuntu's curl 8.18.0 with OpenSSL 3.5.5 does, at the same place and length.

## Context

- BL-1048 / ADR-0350 pad the OpenSSL build's TLS 1.3 hello over TCP (`ClientHelloProfile.PadsTcpHello`, `HandBuiltTlsProvider.ClientSettings.WithPadding`), but leave the TLS 1.2-ceiling hello (`Tls12ClientHelloBuilder`) and the QUIC hello (`QuicClientSettings.CreateOpenSslTlsSettings`) unpadded because neither was measured with a hello of 256 to 511 bytes.
- OpenSSL's `tls_construct_ctos_padding` (`ssl/statem/extensions_clnt.c`) pads whenever `SSL_OP_TLSEXT_PADDING` is set, which `SSL_OP_ALL` includes; whether QUIC's hello is padded in OpenSSL 3.5 is to be measured.
- Measure with `Record-CurlExchange.ps1` in plain TCP mode through WSL as BL-709's Notes describe; QUIC needs a UDP capture (extend the script if needed).

## Acceptance criteria

- [ ] `HandBuiltTlsProviderTests` pins the OpenSSL build's `--tls-max 1.2 --curves X25519` hello's extension order as measured.
- [ ] `Curl.Quic.UnitTests` or `Curl.Networking.UnitTests` pins whether the OpenSSL build's QUIC hello under `--curves X25519` carries `padding`, as measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-10-02: Created.
