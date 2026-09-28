---
id: BL-702
title: Protect TLS 1.2, 1.1 and 1.0 records and derive their keys with the TLS PRF
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-696, BL-673]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-702 — Protect TLS 1.2, 1.1 and 1.0 records and derive their keys with the TLS PRF

## Goal

The hand-built client has the TLS 1.2, 1.1 and 1.0 record layer and key derivation: the TLS PRF (P_SHA256/P_SHA384 for 1.2, MD5+SHA-1 for 1.0/1.1), the extended master secret (RFC 7627), key block expansion, AEAD (AES-GCM, ChaCha20-Poly1305) and CBC-HMAC record protection with explicit IVs for 1.1/1.2, and for TLS 1.0 CBC the 1/n-1 record split that `--ssl-allow-beast` turns off.

## Context

- Design: BL-695's ADR. Consumers: the TLS 1.2 handshake (BL-703), SRP (BL-704), TLS 1.0/1.1 where the OS refuses them (BL-714), `--ssl-allow-beast` (BL-713). BCL `Aes` (CBC), `AesGcm`, `HMACSHA1`/`256`/`384`, `MD5`; ChaCha20-Poly1305 from `Curl.Cryptography.UnitLibrary`.
- References: RFC 5246 sections 5 and 6, RFC 4346 (1.1), RFC 2246 (1.0), RFC 7627, RFC 5288 (GCM suites), RFC 7905 (ChaCha20-Poly1305 suites). The BEAST 1/n-1 split is what OpenSSL does by default (curl's `CURLSSLOPT_ALLOW_BEAST` disables it); cite the OpenSSL source for the exact split.
- Test vectors: PRF outputs computed independently in the test from the RFC definitions (and cross-checked once against `openssl` output recorded in a comment); CBC padding checked in constant time.

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` pin PRF outputs for 1.0/1.1 and 1.2, the extended master secret, key blocks, and record protection round trips for each suite family and version, with the 1/n-1 split on by default for TLS 1.0 CBC and off when asked; a bad MAC or padding fails with `bad_record_mac` without a timing-dependent branch.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
