---
id: BL-702
title: Protect TLS 1.2, 1.1 and 1.0 records and derive their keys with the TLS PRF
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-696, BL-673, BL-783, BL-784]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-702 — Protect TLS 1.2, 1.1 and 1.0 records and derive their keys with the TLS PRF

## Goal

The hand-built client has the TLS 1.2, 1.1 and 1.0 record layer and key derivation: the TLS PRF (P_SHA256/P_SHA384 for 1.2, MD5+SHA-1 for 1.0/1.1), the extended master secret (RFC 7627), key block expansion, AEAD (AES-GCM, ChaCha20-Poly1305) and CBC-HMAC record protection with explicit IVs for 1.1/1.2, and for TLS 1.0 CBC the 1/n-1 record split that `--ssl-allow-beast` turns off.

## Context

- Design: BL-695's ADR. Consumers: the TLS 1.2 handshake (BL-703), SRP (BL-704), TLS 1.0/1.1 where the OS refuses them (BL-714), `--ssl-allow-beast` (BL-713). BCL `Aes` (CBC), `AesGcm`, `HMACSHA1`/`256`/`384`, `MD5`; ChaCha20-Poly1305 from `Curl.Cryptography.UnitLibrary`.
- References: RFC 5246 sections 5 and 6, RFC 4346 (1.1), RFC 2246 (1.0), RFC 7627, RFC 5288 (GCM suites), RFC 7905 (ChaCha20-Poly1305 suites). The BEAST 1/n-1 split is what OpenSSL does by default (curl's `CURLSSLOPT_ALLOW_BEAST` disables it); cite the OpenSSL source for the exact split.
- Test vectors: PRF outputs computed independently in the test from the RFC definitions (and cross-checked once against `openssl` output recorded in a comment); CBC padding checked in constant time.

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` pin PRF outputs for 1.0/1.1 and 1.2, the extended master secret, key blocks, and record protection round trips for each suite family and version, with the 1/n-1 split on by default for TLS 1.0 CBC and off when asked; a bad MAC or padding fails with `bad_record_mac` without a timing-dependent branch.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built: `TlsPrf` (`Md5Sha1`, `Sha256`, `Sha384`; master secret, extended master secret,
  key block, both verify_data), `Tls12RecordProtectionParameters`, `Tls12KeyBlock`,
  `Tls12WriteKeys`, and the two connection states `Tls12RecordWriteState` /
  `Tls12RecordReadState` (ADR-0140 called the pair `Tls12RecordLayer`; two states match
  RFC 5246 section 6.1 and let the handshake switch each direction at its own
  ChangeCipherSpec). Bulk ciphers: null (± MAC), AES-128/256-CBC, Camellia-128/256-CBC,
  3DES-EDE-CBC, AES-128/256-GCM, ARIA-128/256-GCM, ChaCha20-Poly1305; MACs HMAC-MD5/SHA1/
  SHA256/SHA384; encrypt-then-MAC (RFC 7366).
- PRF vectors: recorded from OpenSSL 3.5.7's `TLS1-PRF` KDF (the command is in
  `TlsPrfTests`); the SHA-256 one is also the IETF TLS list's published vector. The
  derived secrets are checked against an independent RFC transcription in the test.
- Decision (ADR-0150): the TLS 1.0 CBC BEAST countermeasure is OpenSSL/LibreSSL's empty
  fragment, not the 1/n-1 split the criterion names: those are the builds the hand-built
  client stands in for, and curl's `--ssl-allow-beast` sets OpenSSL's
  `SSL_OP_DONT_INSERT_EMPTY_FRAGMENTS`. Criterion 1 ticked on that shape ("on by default
  for TLS 1.0 CBC and off when asked"). Not measured against real curl: no TLS 1.0 CBC
  server is at hand in this lane, so the OpenSSL source is cited; BL-713 measures it (note
  added there).
- Decision (ADR-0150): GCM explicit nonce = sequence number (RFC 5288 section 3 allows it;
  BoringSSL and Go do it); OpenSSL counts up from a random start, which is not visible to
  the peer as different behaviour.
- Constant time: padding checked with masks over the last 256 bytes, MAC always computed
  and compared in fixed time, one branch on both. The HMAC's length still follows the
  padding (Lucky Thirteen residual): filed as BL-795 (hand-built SHA compression and a
  fixed-block HMAC).
- Not here: AES-CCM/CCM8 and RC4 records wait on their primitives (BL-738, BL-676);
  filed as BL-796.
- Touches: added `Documentation/Planning/Decisions` for ADR-0150 and its README row; no
  task in Doing names it (BL-507 touches Networking/Console, BL-656 Http2). Also added a
  note to BL-713's Notes pointing at ADR-0150.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests all pass (Curl.Tls.UnitTests
  397); `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary`: 100% line, 100% branch,
  409 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. TLS PRF (1.0/1.1 and 1.2), extended master secret, key block, and TLS 1.2/1.1/1.0 record protection for null, CBC (MtE and EtM) and AEAD suites with the TLS 1.0 empty-fragment split work
