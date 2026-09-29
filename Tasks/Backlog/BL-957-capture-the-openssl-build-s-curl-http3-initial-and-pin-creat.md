---
id: BL-957
title: Capture the OpenSSL build's curl --http3 Initial and pin CreateOpenSslTlsSettings to it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-847]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-957 — Capture the OpenSSL build's curl --http3 Initial and pin CreateOpenSslTlsSettings to it

## Goal

`QuicClientSettings.CreateOpenSslTlsSettings` produces the ClientHello a real Linux curl built with ngtcp2 and OpenSSL 3.5 sends in its first Initial, extension for extension, as measured rather than derived.

## Context

- BL-847 derived the OpenSSL build's QUIC ClientHello from `ClientHelloProfile.OpenSsl` (the TCP capture, BL-787) by ADR-0140's rule: TLS 1.3 parts only, leaving out `renegotiation_info`, `ec_point_formats`, `encrypt_then_mac`, `extended_master_secret` and `post_handshake_auth`, and putting `quic_transport_parameters` last, where OpenSSL's extension table has it. That lane ran on Windows and could not capture a Linux `curl --http3` Initial.
- ADR-0144 section 5 did the same for curl.se's LibreSSL build: capture the first Initial and remove its Initial protection (RFC 9001 section 5.2 derives the keys from the destination connection ID). Extend `Record-CurlExchange.ps1` to receive one UDP datagram if it cannot yet.
- Also check whether that build's hello sends `supported_versions` with TLS 1.3 only, and whether `signature_algorithms` keeps the ML-DSA schemes the client cannot check (they are cut today, as over TCP).

## Acceptance criteria

- [ ] A test in `Curl.Quic.UnitTests` pins the ClientHello `CreateOpenSslTlsSettings` builds against the captured hello's extension order and lists, with the capture's curl and OpenSSL versions named in a comment.
- [ ] `TcpConnectorQuicTests.ConnectMultiplexedAsync_InTheOpenSslBuild_SendsTheOpenSslProfilesTls13ClientHello` expects the measured order.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Quic.UnitLibrary`.

## Notes

Filed by BL-847.

## Log

- 2026-09-29: Created.
