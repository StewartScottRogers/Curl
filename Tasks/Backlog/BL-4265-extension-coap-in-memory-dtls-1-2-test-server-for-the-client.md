---
id: BL-4265
title: Extension CoAP: in-memory DTLS 1.2 test server for the client handshake tests
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4263]
touches: [Curl.Extension.Coap.UnitTests/Dtls/TestServer]
requirement: none
created: 2026-10-29
completed:
---
# BL-4265 — Extension CoAP: in-memory DTLS 1.2 test server for the client handshake tests

## Goal

`Curl.Extension.Coap.UnitTests` has an in-memory DTLS 1.2 server that answers a client over a scripted datagram channel: HelloVerifyRequest with a cookie, then ServerHello, Certificate, ServerKeyExchange (ECDHE signed with ECDSA or RSA), ServerHelloDone, then ChangeCipherSpec and Finished, and echoes protected application data.

## Context

Split from BL-4251 for size (extension 15 of 63, BL-4116). Test code only, for BL-4251's client handshake tests, which run against it. Use `Curl.Tls`'s public TLS 1.2 messages (`Tls12ServerKeyExchange`, `Tls12ClientKeyExchange`, `EcdhKeyShare`, `TlsPrf`, `Finished`), BL-4250's framing (`Curl.Extension.Coap.UnitLibrary/Dtls/Wire`) and BL-4263's record protection. Certificates are generated in memory (`System.Security.Cryptography.X509Certificates.CertificateRequest`), never files, so the tests stay platform-neutral. The transcript hashes the DTLS handshake headers (RFC 6347 4.2.6). The DTLS ClientHello carries a cookie between session_id and cipher_suites (RFC 6347 4.2.1), so `Curl.Tls`'s `ClientHello` cannot parse it as is.

## Acceptance criteria

- [ ] The server lives in `Curl.Extension.Coap.UnitTests/Dtls/TestServer` and its own tests show it answering a hand-built ClientHello with a HelloVerifyRequest and, given the cookie, with the server flight.
- [ ] It can be told to drop a chosen flight once and to replay a chosen record.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
