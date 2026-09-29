---
id: BL-783
title: Hand-build the Camellia block cipher to RFC 3713 for the TLS Camellia suites
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-783 — Hand-build the Camellia block cipher to RFC 3713 for the TLS Camellia suites

## Goal

`Curl.Cryptography.UnitLibrary` has a public `Camellia` type (128-, 192- and 256-bit keys, ECB block operation and CBC) that the hand-built TLS client uses for the Camellia-CBC suites curl's LibreSSL and OpenSSL builds offer.

## Context

- ADR-0140 (BL-695) amends ADR-0118's list with Camellia: curl.se's official Windows build (LibreSSL 4.2.1) offers `00c4 0088 00c0 0084 00be 0045 00ba 0041` in its default ClientHello, and OpenSSL 3.5.5 lists the `*-CAMELLIA*` suites (RFC 5932). The BCL has no Camellia on any platform.
- Follow ADR-0118's API shape and constant-time rules (Camellia's S-boxes are table lookups; state in the XML docs whether the implementation is constant-time, as ADR-0118 requires for Blowfish and CAST-128).
- Consumer: BL-702 (TLS 1.2/1.1/1.0 CBC records).

## Acceptance criteria

- [ ] `Camellia` encrypts and decrypts RFC 3713 Appendix A's test vectors for 128-, 192- and 256-bit keys, pinned in `Curl.Cryptography.UnitTests` with the source cited.
- [ ] CBC over `Camellia` matches RFC 5932's cited vectors or, failing those, a vector recorded from `openssl enc -camellia-128-cbc` with the command in a comment.
- [ ] A wrong key length throws `ArgumentException`; `Dispose` zeroes the key schedule and use after `Dispose` throws `ObjectDisposedException`.
- [ ] The library meets the quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30).

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
