---
id: BL-4283
title: Extension CoAP: offer TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8 in DTLS 1.2
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4251]
touches: [Curl.Extension.Coap.UnitLibrary/Dtls/Record, Curl.Extension.Coap.UnitLibrary/Dtls/Handshake/DtlsClientHandshake.cs, Curl.Extension.Coap.UnitLibrary/Dtls/Handshake/DtlsClientHello.cs, Curl.Extension.Coap.UnitTests/Dtls/Handshake, Curl.Extension.Coap.UnitTests/Dtls/Record, Curl.Extension.Coap.UnitTests/Dtls/TestServer/DtlsTestServer.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4283 — Extension CoAP: offer TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8 in DTLS 1.2

## Goal

The CoAP extension's DTLS 1.2 client offers TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8 (RFC 7251), CoAP's baseline suite for certificates (RFC 7252 section 9.1.3.2), and protects records with it when the server picks it.

## Context

BL-4251 (ADR-1078) offers only the two AES-128-GCM suites, because `Dtls/Record` holds only AES-128-GCM protection. `System.Security.Cryptography.AesCcm` takes an 8-byte tag. Add CCM_8 record protection beside `DtlsAes128GcmRecordProtector` (RFC 6655 nonce: 4-byte salt, 8-byte explicit part), its key block (16-byte key, 4-byte IV), offer the suite in `DtlsClientHello`, and pick the record protection by the ServerHello's suite in `DtlsClientHandshake.FinishAsync`. The test server needs to negotiate it.

## Acceptance criteria

- [ ] Unit tests run a full handshake with the CCM_8 suite against the in-memory test server and round-trip application data.
- [ ] ADR-1078 says the suite is offered.
- [ ] Curl.Extension.Coap.UnitLibrary meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 01ae170e 42bc4207 cf4c1338 3d836d5e.
