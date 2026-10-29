---
id: BL-4266
title: Extension CoAP: protect DTLS 1.2 records with AES-128-GCM
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Coap.UnitLibrary/Dtls/Record, Curl.Extension.Coap.UnitTests/Dtls/Record, Curl.Extension.Coap.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4266 — Extension CoAP: protect DTLS 1.2 records with AES-128-GCM

## Goal

`Curl.Extension.Coap.UnitLibrary` protects and unprotects DTLS 1.2 records with AES-128-GCM (RFC 5288, RFC 6347 section 4.1.2), from a key block derived with `TlsPrf`, dropping replayed and forged records.

## Context

Split from BL-4251 for size (extension 15 of 63, BL-4116). Extension rules: ADR-0999; it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, and is tested against the RFCs, not curl. Builds on BL-4250's framing in `Curl.Extension.Coap.UnitLibrary/Dtls/Wire` (`DtlsRecordHeader`, `DtlsReplayWindow`).

Namespace `Curl.Extension.Coap.Dtls.Record`. Use `Curl.Tls`'s `TlsPrf.ComputeKeyBlock` and `AesGcmTlsAead`. The nonce is the 4-byte write IV followed by the 8-byte explicit part (epoch and sequence number, sent before the ciphertext); the additional data is epoch || sequence_number || type || version || length (RFC 6347 4.1.2.1, RFC 5246 6.2.3.3). BL-4251's handshake and BL-4265's test server both use it.

## Acceptance criteria

- [x] Unit tests derive client and server write keys from a master secret and both randoms and round-trip application data in both directions.
- [x] Unit tests show a replayed record dropped, a record with a changed byte dropped, and an epoch-0 record passed through unprotected.
- [x] The changed library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added `Curl.Extension.Coap.UnitLibrary/CLAUDE.md` to touches so the project document describes `Dtls/Record/`; no task in Doing on origin/work/dark-factory names it.
- Decided: the record layer uses the BCL `AesGcm` directly rather than `Curl.Tls`'s `AesGcmTlsAead`, which is internal to `Curl.Tls` with no InternalsVisibleTo for this extension; widening `Curl.Tls` was outside touches and `AesGcmTlsAead` is a thin wrapper over the same `AesGcm`. Keys come from `TlsPrf.Sha256.ComputeKeyBlock` and `Tls12KeyBlock.Partition` as the task asks.
- Decided: one protector and one unprotector per epoch, sequence numbers from 0; an epoch-0 record passes through without a replay check (handshake retransmission is the handshake layer's concern); bad records are dropped silently (RFC 6347 section 4.1.2.7).
- Pipeline `feature` run directly: four small types, no seams to plan beyond the task's Context.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.Coap.UnitLibrary`: 100% line, 100% branch, 0 failing members. `dotnet build` clean, fast tests green on Windows; the new tests are pure computation and platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: cdf3eb35 fa37309c 54cafd17 3d836d5e.
- 2026-10-29: Doing -> Done. DTLS 1.2 records are protected and unprotected with AES-128-GCM from a TlsPrf key block, dropping replayed and forged records
