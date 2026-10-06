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
completed: 2026-10-01
---
# BL-957 — Capture the OpenSSL build's curl --http3 Initial and pin CreateOpenSslTlsSettings to it

## Goal

`QuicClientSettings.CreateOpenSslTlsSettings` produces the ClientHello a real Linux curl built with ngtcp2 and OpenSSL 3.5 sends in its first Initial, extension for extension, as measured rather than derived.

## Context

- BL-847 derived the OpenSSL build's QUIC ClientHello from `ClientHelloProfile.OpenSsl` (the TCP capture, BL-787) by ADR-0140's rule: TLS 1.3 parts only, leaving out `renegotiation_info`, `ec_point_formats`, `encrypt_then_mac`, `extended_master_secret` and `post_handshake_auth`, and putting `quic_transport_parameters` last, where OpenSSL's extension table has it. That lane ran on Windows and could not capture a Linux `curl --http3` Initial.
- ADR-0144 section 5 did the same for curl.se's LibreSSL build: capture the first Initial and remove its Initial protection (RFC 9001 section 5.2 derives the keys from the destination connection ID). Extend `Record-CurlExchange.ps1` to receive one UDP datagram if it cannot yet.
- Also check whether that build's hello sends `supported_versions` with TLS 1.3 only, and whether `signature_algorithms` keeps the ML-DSA schemes the client cannot check (they are cut today, as over TCP).

## Acceptance criteria

- [x] A test in `Curl.Quic.UnitTests` pins the ClientHello `CreateOpenSslTlsSettings` builds against the captured hello's extension order and lists, with the capture's curl and OpenSSL versions named in a comment.
- [x] `TcpConnectorQuicTests.ConnectMultiplexedAsync_InTheOpenSslBuild_SendsTheOpenSslProfilesTls13ClientHello` expects the measured order.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Quic.UnitLibrary`.

## Notes

Filed by BL-847.

- Captured with Docker's `fedora:latest` curl 8.18.0 (OpenSSL 3.5.7, ngtcp2 1.22.1, nghttp3 1.18.0): Ubuntu's curl has no HTTP/3. `Record-CurlExchange.ps1 -NoServer -UdpSink -Port 4433 -ListenAddress 0.0.0.0 -Curl docker -CurlArgs run,--rm,fedora:latest,curl,--http3-only,...` already received the datagrams, so the script needed no change. The Initial protection was removed by a throwaway C# file-based app over `QuicPacketProtection.CreateClientInitial` and `QuicFrameCodec.Decode`; ngtcp2 scatters the 1,539-byte hello's CRYPTO frames across two Initials out of order.
- Also recorded the same container's TCP hello, to separate what QUIC changes from Fedora's crypto policy. QUIC changes only: `quic_transport_parameters` first in place of `renegotiation_info`, TLS 1.3-only `supported_versions` and suites, signature schemes without `0303 0301`, no session ID. `ec_point_formats`, `encrypt_then_mac`, `extended_master_secret` and `post_handshake_auth` stay. ML-DSA schemes stay (they already passed the old filter).
- Decision (ADR-0291): apply that change to `ClientHelloProfile.OpenSsl` (Ubuntu's lists), not take Fedora's policy lists; signature schemes cut to TLS 1.3 schemes plus RSA PKCS #1 v1.5 over SHA-2.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. CreateOpenSslTlsSettings sends the QUIC ClientHello measured from Fedora's curl 8.18.0 (OpenSSL 3.5.7, ngtcp2 1.22.1): transport parameters first, the profile's TLS 1.2 extensions kept, SHA-1/SHA-224/DSA schemes cut
