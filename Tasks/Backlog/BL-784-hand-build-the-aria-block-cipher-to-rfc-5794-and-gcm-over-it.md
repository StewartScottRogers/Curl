---
id: BL-784
title: Hand-build the ARIA block cipher to RFC 5794 and GCM over it for the TLS ARIA-GCM suites
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-784 — Hand-build the ARIA block cipher to RFC 5794 and GCM over it for the TLS ARIA-GCM suites

## Goal

`Curl.Cryptography.UnitLibrary` has a public `Aria` block cipher (128-, 192- and 256-bit keys) and an `AeadAriaGcm` AEAD (GCM, NIST SP 800-38D, over ARIA, 16-byte tags) that the hand-built TLS client uses for OpenSSL's `*-ARIA*-GCM-*` suites (RFC 6209).

## Context

- ADR-0140 (BL-695) amends ADR-0118's list with ARIA and ARIA-GCM: OpenSSL 3.5.5's `openssl ciphers -v 'ALL:COMPLEMENTOFALL'` (measured 2026-09-28) lists `ARIA128-GCM-SHA256`, `ECDHE-ECDSA-ARIA256-GCM-SHA384` and the rest. The BCL has no ARIA, and `AesGcm` is AES-only, so GCM's GHASH and counter mode are built here over the block cipher.
- Follow ADR-0118's API shape (`Aead…` prefix, `Try…` decrypt returning `false` for a bad tag with the destination zeroed, span-based) and constant-time rules (ARIA's S-boxes are table lookups; GHASH must not branch on secrets; say in the XML docs what is constant-time).
- Consumer: BL-702 (TLS 1.2 AEAD records).

## Acceptance criteria

- [ ] `Aria` matches RFC 5794 Appendix A's vectors for all three key sizes.
- [ ] `AeadAriaGcm` matches a published ARIA-GCM vector (RFC 6209's references, or the Korean KISA ARIA-GCM test vectors) or, failing those, a vector recorded from a TLS 1.2 ARIA-GCM record exchanged between `openssl s_client` and `openssl s_server` with `-keylogfile`, the commands in a comment; a flipped bit in the tag, ciphertext or associated data returns `false`.
- [ ] GCM's generic part is checked by running it over the BCL's AES and matching `AesGcm` on random inputs.
- [ ] The library meets the quality gates.

## Notes

## Log

- 2026-09-28: Created.
