---
id: BL-4583
title: Fix Coap conformance: Coap:RFC6347-4.2.1-1 (ClientKeyExchange carries the ECDHE point with two length prefixes)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Handshake/DtlsClientHandshake.cs, Curl.Extended.Protocol.Coap.UnitTests/Dtls, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4583 — Fix Coap conformance: Coap:RFC6347-4.2.1-1 (ClientKeyExchange carries the ECDHE point with two length prefixes)

## Goal

Curl's `coaps://` ClientKeyExchange carries the ECDHE point as RFC 8422 section 5.7's `ecdh_Yc<1..255>` with one
length byte, so a DTLS 1.2 server written from the RFCs completes the handshake.

## Context

Found by BL-4573's `DtlsReferenceServer`. `DtlsClientHandshake.ExchangeKeysAsync` passes
`[(byte)clientShare.PublicKey.Length, .. clientShare.PublicKey]` to `Tls12ClientKeyExchange`, whose `Encode` writes
the opaque<1..255> length itself, so the body is 66, 65, 04, X, Y: two length bytes. The library's own test server
reads it the same wrong way. Pass `clientShare.PublicKey` alone (and fix the test server's reading). Cases listed in
`KnownFailures.txt` against this task: Coap:RFC6347-4.2.1-1, Coap:RFC6347-4.1-1, Coap:RFC7252-9.1-1,
Coap:RFC7252-9.1-3, Coap:RFC7252-9.1-4.

## Acceptance criteria

- [x] The cases in `Curl.Conformance.Extended.UnitTests/Coap/Dtls/` pass against `DtlsReferenceServer`.
- [x] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests` pins the ClientKeyExchange body as one length
      byte and the 65-byte point (ADR-0482).
- [x] The five BL-4577 lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.

## Notes

- `DtlsClientHandshake.ExchangeKeysAsync` now passes the bare point to `Tls12ClientKeyExchange`, whose `Encode` writes the one ecdh_Yc length byte; the unit test server and `DtlsTestServerTests` read and build it the same way. `RunAsync_Rfc8422Section5_7_ClientKeyExchange_CarriesTheUncompressedPointBehindOneLengthByte` pins the 66-byte body (65, 04, X, Y).
- `KnownFailures.txt` also loses `Amqp0:AMQP091-1.5.2.3-1 BL-4564`: that case passes since eaf5b59f4 (channel.flow answered with flow-ok), and the ratchet fails `Flow_FromTheBroker_IsAnsweredWithFlowOk` while the line stays. Measured here by restoring the line and running the conformance tests.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: f298e558 bff304f3 34f35708.
- 2026-11-02: Doing -> Done. ClientKeyExchange carries the ECDHE point behind one length byte; the five coaps:// cases pass against DtlsReferenceServer
