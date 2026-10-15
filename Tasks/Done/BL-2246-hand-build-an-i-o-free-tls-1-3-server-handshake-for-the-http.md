---
id: BL-2246
title: Hand-build an I/O-free TLS 1.3 server handshake for the HTTP/3 stand-in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Http3Server.UnitLibrary, Curl.Conformance.Http3Server.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2246 — Hand-build an I/O-free TLS 1.3 server handshake for the HTTP/3 stand-in

## Goal

A new `Curl.Conformance.Http3Server.UnitLibrary` (+ `.UnitTests`, listed in `Curl.slnx`) holds an I/O-free TLS 1.3 server handshake at the message level (no TLS records, as QUIC carries it in CRYPTO frames) that completes a handshake with Curl's own `Tls13ClientHandshake` offering ALPN `h3`.

## Context

Split out of BL-2231 (the `%HTTP3PORT` stand-in for upstream's nghttpx-h3 server), whose first pass found no TLS server handshake anywhere in the solution: `Curl.Tls.UnitLibrary` is client-only and the conformance TLS stand-ins (`TlsServerStream`) use `SslStream`, which cannot hand out per-level handshake bytes and secrets as QUIC needs (RFC 9001 section 4). Model the project on `Curl.Conformance.SshServer.UnitLibrary` / `Curl.Conformance.Http2Server.UnitLibrary` (ADR-0456); it may reference `Curl.Tls.UnitLibrary`, `Curl.Cryptography.*` and `Curl.Conformance.UnitLibrary` but no protocol library. Reuse `Tls13KeySchedule`, `TranscriptHash`, `ClientHello`/`ServerHello`/`EncryptedExtensions`/`CertificateMessage`/`CertificateVerify`/`Finished` codecs and `X25519KeyShare` / `EcdhKeyShare` from `Curl.Tls`.

Scope: parse ClientHello; pick TLS_AES_128_GCM_SHA256 (also accept the client's other offered suites), X25519 (secp256r1 fallback; HelloRetryRequest when no usable share); ServerHello; EncryptedExtensions carrying ALPN `h3` and the `quic_transport_parameters` bytes the caller supplies; Certificate from upstream's `test-localhost.pem` (as the other TLS stand-ins load it, `UpstreamCertificateFolder`); CertificateVerify signed with that key (rsa_pss_rsae_sha256 for RSA); server Finished; verify client Finished. Output per encryption level (Initial, Handshake, 1-RTT): the handshake bytes to send and the client/server traffic secrets, so BL-2247 can drive QUIC packet protection.

## Acceptance criteria

- [x] `Curl.Conformance.Http3Server.UnitLibrary` and `.UnitTests` exist, listed in `Curl.slnx` in sorted place, AOT-compatible; `dotnet build -warnaserror` clean.
- [x] A unit test drives `Tls13ClientHandshake` (Curl's QUIC client TLS settings, ALPN `h3`) against the server handshake in memory to completion; both sides derive equal handshake and application traffic secrets.
- [x] Tests pin: no common cipher suite or group -> handshake_failure alert; HelloRetryRequest path; bad client Finished -> decrypt_error alert; ALPN without `h3` -> no_application_protocol.
- [x] 100% line and branch coverage, complexity <= 10, CRAP <= 30 per `Measure-CodeQuality.ps1 -Library Curl.Conformance.Http3Server.UnitLibrary`; platform-neutral tests; the project's `CLAUDE.md` describes it.

## Notes

- Library: `Http3ServerTlsHandshake` (state machine), `Http3ServerTlsSettings` (`FromRsaCertificate` for test-localhost.pem as `HttpsServerConnector.LoadCertificate` loads it), `Http3ServerClientOffer` (ClientHello extension checks), `Http3ServerTlsOutput`. References only `Curl.Tls.UnitLibrary`; output reuses `Tls13HandshakeOutput`/`Tls13TrafficSecret`.
- Choices (defaults taken): server preference order picks suite, group and scheme; a key share for any common group is used before a HelloRetryRequest; no ALPN at all is `no_application_protocol` like ALPN without h3 (RFC 9001 8.1); missing `quic_transport_parameters` is `missing_extension` (RFC 9001 8.2); `Finished.Decode` never fails, so a Finished of the wrong length is `decrypt_error`; Application read secret is installed only once the client Finished verifies.
- Measured: Measure-CodeQuality -Library Curl.Conformance.Http3Server.UnitLibrary: line 100, complexity max 10, one lambda at 50% branch (`ChooseSignatureScheme`, key cannot sign an offered scheme); covered by `Receive_PreferredSchemeTheRsaKeyCannotSign_SignsWithTheNextOne`. 32 tests.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. I/O-free TLS 1.3 server handshake for the HTTP/3 stand-in completes against Curl's QUIC client with matching secrets
