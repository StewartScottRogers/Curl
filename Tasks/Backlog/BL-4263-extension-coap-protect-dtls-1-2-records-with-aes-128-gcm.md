---
id: BL-4263
title: Extension CoAP: protect DTLS 1.2 records with AES-128-GCM
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Coap.UnitLibrary/Dtls/Record, Curl.Extension.Coap.UnitTests/Dtls/Record]
requirement: none
created: 2026-10-29
completed:
---
# BL-4263 — Extension CoAP: protect DTLS 1.2 records with AES-128-GCM

## Goal

`Curl.Extension.Coap.UnitLibrary` protects and unprotects DTLS 1.2 records with AES-128-GCM (RFC 5288, RFC 6347 section 4.1.2), from a key block derived with `TlsPrf`, dropping replayed and forged records.

## Context

Split from BL-4251 for size (extension 15 of 63, BL-4116). Extension rules: ADR-0999; it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, and is tested against the RFCs, not curl. Builds on BL-4250's framing in `Curl.Extension.Coap.UnitLibrary/Dtls/Wire` (`DtlsRecordHeader`, `DtlsReplayWindow`).

Namespace `Curl.Extension.Coap.Dtls.Record`. Use `Curl.Tls`'s `TlsPrf.ComputeKeyBlock` and `AesGcmTlsAead`. The nonce is the 4-byte write IV followed by the 8-byte explicit part (epoch and sequence number, sent before the ciphertext); the additional data is epoch || sequence_number || type || version || length (RFC 6347 4.1.2.1, RFC 5246 6.2.3.3). BL-4251's handshake and BL-4265's test server both use it.

## Acceptance criteria

- [ ] Unit tests derive client and server write keys from a master secret and both randoms and round-trip application data in both directions.
- [ ] Unit tests show a replayed record dropped, a record with a changed byte dropped, and an epoch-0 record passed through unprotected.
- [ ] The changed library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
