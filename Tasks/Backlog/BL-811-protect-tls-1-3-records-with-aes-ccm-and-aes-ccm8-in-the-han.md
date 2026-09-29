---
id: BL-811
title: Protect TLS 1.3 records with AES-CCM and AES-CCM8 in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-700, BL-738]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-811 — Protect TLS 1.3 records with AES-CCM and AES-CCM8 in the hand-built TLS client

## Goal

`Tls13RecordProtection` protects and opens records of `TLS_AES_128_CCM_SHA256` (0x1304) and `TLS_AES_128_CCM_8_SHA256` (0x1305) with BL-738's hand-built `AeadAesCcm`, so a TLS 1.3 connection negotiating either suite completes instead of being refused before anything is sent.

## Context

- BL-700 built the TLS 1.3 record layer for the GCM and ChaCha20-Poly1305 suites; `Tls13RecordProtection.CanProtect` answers false for 0x1304 and 0x1305, and `Tls13ClientConnection` refuses them before sending the ClientHello (test `SuiteWhoseRecordsCannotBeProtectedIsRefusedBeforeAnythingIsSent`).
- CCM8's tag is 8 bytes, not 16: `Tls13RecordProtection`'s tag length (and so the shortest valid fragment and the `bad_record_mac` check) becomes per-suite.
- The AEAD goes behind `ITlsAead` like `AesGcmTlsAead`; RFC 8446 section 5.3 nonces and section 5.2 additional data are unchanged.
- ADR-0140 lists the suites; OpenSSL enables CCM only when named (`--tls13-ciphers`, BL-709).

## Acceptance criteria

- [ ] `Tls13RecordProtection.CanProtect` is true for 0x1304 and 0x1305, and `Curl.Tls.UnitTests` exchange application data both ways with the in-memory server over a pipe for each (the `ApplicationDataCrossesBothWaysWithEachCipherSuite` rows).
- [ ] A CCM8 record whose fragment is shorter than 8 bytes, or whose tag is corrupted, fails with `bad_record_mac`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-700, which could not cover the CCM suites before BL-738 builds AES-CCM.

## Log

- 2026-09-28: Created.
