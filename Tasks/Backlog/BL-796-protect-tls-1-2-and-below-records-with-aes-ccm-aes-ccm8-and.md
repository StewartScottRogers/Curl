---
id: BL-796
title: Protect TLS 1.2 and below records with AES-CCM, AES-CCM8 and RC4
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-702, BL-738, BL-676]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-796 — Protect TLS 1.2 and below records with AES-CCM, AES-CCM8 and RC4

## Goal

`Tls12BulkCipher` gains AES-128-CCM, AES-256-CCM, AES-128-CCM8, AES-256-CCM8 (RFC 6655, RFC 7251) and RC4-128 (RFC 5246's stream cipher record, LibreSSL's `RC4-SHA`, `RC4-MD5` and `ECDHE-*-RC4-SHA` suites), so the record layer covers every TLS 1.2 and below bulk cipher ADR-0140 lists.

## Context

- BL-702 built `Tls12RecordWriteState`, `Tls12RecordReadState` and the null, CBC and AEAD (GCM, ChaCha20-Poly1305) layouts; ADR-0150 covers its choices.
- AES-CCM is the hand-built `AeadAesCcm` (BL-738; the BCL's `AesCcm` is missing on macOS). CCM records use GCM's layout: a 4-byte salt from the key block and an 8-byte explicit nonce (RFC 6655 section 3), with a 16-byte tag, or 8 bytes for CCM8.
- RC4 is the hand-built `Rc4` (BL-676). A stream cipher record is `E(content || MAC)` with the key stream running on from record to record (RFC 5246 section 6.2.3.1); the key block carries no IV.

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` round-trip records for each new cipher at each version it is valid in, pin one record per cipher byte for byte against `AeadAesCcm` or `Rc4` driven by the test, and show a flipped bit is `bad_record_mac`; CCM below TLS 1.2 is refused.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
