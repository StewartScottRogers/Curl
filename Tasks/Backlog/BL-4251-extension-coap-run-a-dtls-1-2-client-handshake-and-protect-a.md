---
id: BL-4251
title: Extension CoAP: run a DTLS 1.2 client handshake and protect application data
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4250, BL-4249, BL-4263, BL-4264, BL-4265]
touches: [Curl.Extension.Coap.UnitLibrary/Dtls/Handshake, Curl.Extension.Coap.UnitTests/Dtls/Handshake]
requirement: none
created: 2026-10-29
completed:
---
# BL-4251 — Extension CoAP: run a DTLS 1.2 client handshake and protect application data

## Goal

A DTLS 1.2 client in `Curl.Extension.Coap.UnitLibrary` completes a full handshake over a datagram channel (cookie exchange, ECDHE with ECDSA or RSA, AES-128-GCM), verifies the server's chain through BL-4249's seam, retransmits flights on its timer, and sends and receives protected application data.

## Context

Part of BL-4116 (extension 15 of 63, split for size). Extension rules: ADR-0999 and the lane rules "Extensions": it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, never a `Curl.Protocol.*` library or another extension, is tested against the RFCs, not curl, and never changes what curl does. Held to the four quality gates.

Builds on BL-4250's framing. Use `Curl.Tls.UnitLibrary`'s public TLS 1.2 pieces (`ClientHello` and extensions, `Tls12ServerKeyExchange`, `Tls12ClientKeyExchange`, `EcdhKeyShare`, `TlsPrf`, `AesGcmTlsAead`, `Finished`, `ExtendedMasterSecretExtension`); if `Tls12ClientHandshake` cannot take DTLS framing, write the state machine here rather than change `Curl.Tls`. The transcript hashes the DTLS handshake headers (RFC 6347 section 4.2.6); the ClientHello is resent with the cookie (4.2.1); flights retransmit on a doubling timer from 1 s (4.2.4) driven by `TimeProvider`. Verification failure is exit 60, other handshake failures exit 35. Namespace `Curl.Extension.Coap.Dtls.Handshake`.

## Acceptance criteria

- [ ] Unit tests run the client against an in-memory DTLS 1.2 test server (in the test project) through a full handshake, each step named after its RFC 6347 or RFC 5246 section.
- [ ] Unit tests cover the HelloVerifyRequest cookie round trip, a lost flight retransmitted on the timer with a fake `TimeProvider`, and the handshake giving up after its retransmission limit.
- [ ] Unit tests show an untrusted chain failing with exit 60 and `-k` accepting it, through BL-4249's seam.
- [ ] Application data round-trips protected with AES-128-GCM and a replayed record is dropped.
- [ ] The changed library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-29 (lane 1, sizing pass before any code): too big for one run - a hand-built DTLS 1.2 client, its record protection, its retransmission timer and an in-memory server, each held to 100% coverage. Split out BL-4263 (AES-128-GCM record protection, Dtls/Record), BL-4264 (flight retransmission timer, Dtls/Retransmission) and BL-4265 (in-memory test server, test project Dtls/TestServer, after BL-4263). This task keeps the handshake state machine in Dtls/Handshake and its tests. Hooks checked: ITlsProvider.VerifyServerCertificateChain (BL-4249), TlsPrf, AesGcmTlsAead, EcdhKeyShare, Tls12ServerKeyExchange, Tls12ClientKeyExchange, Finished and the Dtls/Wire framing all exist. The DTLS ClientHello carries a cookie, so it is encoded here rather than through Curl.Tls ClientHello. BL-4262 (CertificateCheckingTlsProvider forwarding the seam) matters only to BL-4252 wiring coaps://, not to these unit tests.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b5945eb1 9c67082b 4ee129e3 a7e33c4b 54cafd17 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4263 (record protection), BL-4264 (retransmission timer) and BL-4265 (test server)
