---
id: BL-4265
title: Extension CoAP: in-memory DTLS 1.2 test server for the client handshake tests
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4266]
touches: [Curl.Extension.Coap.UnitTests/Dtls/TestServer]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4265 — Extension CoAP: in-memory DTLS 1.2 test server for the client handshake tests

## Goal

`Curl.Extension.Coap.UnitTests` has an in-memory DTLS 1.2 server that answers a client over a scripted datagram channel: HelloVerifyRequest with a cookie, then ServerHello, Certificate, ServerKeyExchange (ECDHE signed with ECDSA or RSA), ServerHelloDone, then ChangeCipherSpec and Finished, and echoes protected application data.

## Context

Split from BL-4251 for size (extension 15 of 63, BL-4116). Test code only, for BL-4251's client handshake tests, which run against it. Use `Curl.Tls`'s public TLS 1.2 messages (`Tls12ServerKeyExchange`, `Tls12ClientKeyExchange`, `EcdhKeyShare`, `TlsPrf`, `Finished`), BL-4250's framing (`Curl.Extension.Coap.UnitLibrary/Dtls/Wire`) and BL-4266's record protection. Certificates are generated in memory (`System.Security.Cryptography.X509Certificates.CertificateRequest`), never files, so the tests stay platform-neutral. The transcript hashes the DTLS handshake headers (RFC 6347 4.2.6). The DTLS ClientHello carries a cookie between session_id and cipher_suites (RFC 6347 4.2.1), so `Curl.Tls`'s `ClientHello` cannot parse it as is.

## Acceptance criteria

- [x] The server lives in `Curl.Extension.Coap.UnitTests/Dtls/TestServer` and its own tests show it answering a hand-built ClientHello with a HelloVerifyRequest and, given the cookie, with the server flight.
- [x] It can be told to drop a chosen flight once and to replay a chosen record.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decided: the RSA server signs with rsa_pss_rsae_sha256 (0x0804, valid in TLS 1.2 per RFC 8446 4.2.3) because Curl.Tls signs only TLS 1.3 scheme rules through its public API; the server ignores extended_master_secret and sends one record per datagram. The ClientHello is parsed by hand (Curl.Tls TlsReader is internal). A retransmitted ClientHello, key exchange or Finished gets the cached flight again.
- Test-only change: no production library touched, so no quality run.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 8678a52f 13fba537 3d836d5e.
- 2026-10-29: Doing -> Done. In-memory DTLS 1.2 test server with drop-once and replay-record controls
