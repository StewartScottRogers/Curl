---
id: BL-784
title: Hand-build the ARIA block cipher to RFC 5794 and GCM over it for the TLS ARIA-GCM suites
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-784 — Hand-build the ARIA block cipher to RFC 5794 and GCM over it for the TLS ARIA-GCM suites

## Goal

`Curl.Cryptography.UnitLibrary` has a public `Aria` block cipher (128-, 192- and 256-bit keys) and an `AeadAriaGcm` AEAD (GCM, NIST SP 800-38D, over ARIA, 16-byte tags) that the hand-built TLS client uses for OpenSSL's `*-ARIA*-GCM-*` suites (RFC 6209).

## Context

- ADR-0140 (BL-695) amends ADR-0118's list with ARIA and ARIA-GCM: OpenSSL 3.5.5's `openssl ciphers -v 'ALL:COMPLEMENTOFALL'` (measured 2026-09-28) lists `ARIA128-GCM-SHA256`, `ECDHE-ECDSA-ARIA256-GCM-SHA384` and the rest. The BCL has no ARIA, and `AesGcm` is AES-only, so GCM's GHASH and counter mode are built here over the block cipher.
- Follow ADR-0118's API shape (`Aead…` prefix, `Try…` decrypt returning `false` for a bad tag with the destination zeroed, span-based) and constant-time rules (ARIA's S-boxes are table lookups; GHASH must not branch on secrets; say in the XML docs what is constant-time).
- Consumer: BL-702 (TLS 1.2 AEAD records).

## Acceptance criteria

- [x] `Aria` matches RFC 5794 Appendix A's vectors for all three key sizes.
- [x] `AeadAriaGcm` matches a published ARIA-GCM vector (RFC 6209's references, or the Korean KISA ARIA-GCM test vectors) or, failing those, a vector recorded from a TLS 1.2 ARIA-GCM record exchanged between `openssl s_client` and `openssl s_server` with `-keylogfile`, the commands in a comment; a flipped bit in the tag, ciphertext or associated data returns `false`.
- [x] GCM's generic part is checked by running it over the BCL's AES and matching `AesGcm` on random inputs.
- [x] The library meets the quality gates.

## Notes

- Built: `Aria` (RFC 5794, S-boxes copied from the RFC's section 2.4.2 tables), the
  internal `IBlockCipher` and `GaloisCounterMode` (NIST SP 800-38D, 12-byte nonce,
  16-byte tag, constant-time bitwise GHASH), and `AeadAriaGcm` over them. Decisions in
  ADR-0146: ARIA indexes its fixed S-boxes (not constant-time, as OpenSSL's ARIA), GHASH
  is constant-time, GCM takes only the 12-byte nonce TLS and the BCL's `AesGcm` use.
- ARIA-GCM vector: RFC 8269 Appendix A.2 (ARIA-GCM for SRTP) publishes 128- and 256-bit
  ARIA-GCM vectors; its salt is all zero, so the nonce is the published IV. Used those
  rather than an `openssl s_server` recording.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0146; no task in `Doing`
  names it.
- The quality audit also failed `Camellia`'s constructor (BL-783) on compiled complexity
  16; split its subkey expansion into `ExpandEncryptionSubkeys` and computed the round
  groups from the layout length, so the library now has 0 failing members.
- Verified: `dotnet build` clean, `dotnet format` clean, fast tests green (482 in
  `Curl.Cryptography.UnitTests`), `Measure-CodeQuality.ps1 -Library
  Curl.Cryptography.UnitLibrary` 100% line, 100% branch, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Aria (RFC 5794) and AeadAriaGcm (GCM over ARIA, RFC 8269 vectors, checked against AesGcm over AES) are in Curl.Cryptography
