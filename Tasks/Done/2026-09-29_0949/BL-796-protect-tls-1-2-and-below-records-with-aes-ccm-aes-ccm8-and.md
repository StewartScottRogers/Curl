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
completed: 2026-09-29
---
# BL-796 — Protect TLS 1.2 and below records with AES-CCM, AES-CCM8 and RC4

## Goal

`Tls12BulkCipher` gains AES-128-CCM, AES-256-CCM, AES-128-CCM8, AES-256-CCM8 (RFC 6655, RFC 7251) and RC4-128 (RFC 5246's stream cipher record, LibreSSL's `RC4-SHA`, `RC4-MD5` and `ECDHE-*-RC4-SHA` suites), so the record layer covers every TLS 1.2 and below bulk cipher ADR-0140 lists.

## Context

- BL-702 built `Tls12RecordWriteState`, `Tls12RecordReadState` and the null, CBC and AEAD (GCM, ChaCha20-Poly1305) layouts; ADR-0150 covers its choices.
- AES-CCM is the hand-built `AeadAesCcm` (BL-738; the BCL's `AesCcm` is missing on macOS). CCM records use GCM's layout: a 4-byte salt from the key block and an 8-byte explicit nonce (RFC 6655 section 3), with a 16-byte tag, or 8 bytes for CCM8.
- RC4 is the hand-built `Rc4` (BL-676). A stream cipher record is `E(content || MAC)` with the key stream running on from record to record (RFC 5246 section 6.2.3.1); the key block carries no IV.

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` round-trip records for each new cipher at each version it is valid in, pin one record per cipher byte for byte against `AeadAesCcm` or `Rc4` driven by the test, and show a flipped bit is `bad_record_mac`; CCM below TLS 1.2 is refused.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: append `Aes128Ccm`, `Aes256Ccm`, `Aes128Ccm8`, `Aes256Ccm8` and `Rc4128` to `Tls12BulkCipher` (appended, so existing values keep their numbers); CCM reuses the GCM explicit-nonce AEAD layout with a per-cipher tag length (16, or 8 for CCM8) carried in `Tls12RecordProtectionParameters`' shape table; RC4 gets a new `Tls12CipherMode.Stream` and `Tls12StreamRecordCipher` (`E(content || MAC)`, one `Rc4` instance per direction so the keystream runs on from record to record, no IV in the key block).
- `AesCcmTlsAead` adapts `Curl.Cryptography`'s `AeadAesCcm` to `ITlsAead`; the tag length is the length of the `tag` span, so `Tls12AeadRecordCipher` now takes `tagLength` instead of a fixed 16.
- Choice: RC4 is accepted with any record MAC in TLS 1.0, 1.1 and 1.2 (RFC 7465 forbids offering it, but ADR-0140 lists LibreSSL's RC4 suites, and LibreSSL negotiates them at every version); RC4 without a MAC is refused like CBC without one. CCM, like every AEAD, is refused below TLS 1.2.
- Choice: CCM's explicit nonce is the sequence number, as GCM's already is (RFC 6655 section 3 allows it, as RFC 5288 does for GCM); no new ADR, ADR-0150's GCM reasoning covers it.
- The suites themselves (code points in `Tls12CipherSuite`) are BL-801's; `Tls12CipherSuite`'s summary now names its bulk ciphers rather than claiming every cipher the record layer protects.
- Verified: `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` 100% line, 100% branch, 0 failing members (worst CRAP 10); `dotnet build Curl.slnx -warnaserror` clean; fast tests green.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. TLS 1.2 and below records are protected with AES-128/256-CCM, CCM8 and RC4-128
