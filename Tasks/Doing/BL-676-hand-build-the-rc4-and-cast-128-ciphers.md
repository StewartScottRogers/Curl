---
id: BL-676
title: Hand-build the RC4 and CAST-128 ciphers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-676 — Hand-build the RC4 and CAST-128 ciphers

## Goal

`Curl.Cryptography.UnitLibrary` provides the RC4 stream cipher (with the keystream discard SSH's `arcfour128` needs) and the CAST-128 block cipher (with CBC), matching their published vectors.

## Context

- Consumers: SSH `arcfour`, `arcfour128` (RFC 4345: discard the first 1536 bytes) and `cast128-cbc`, all offered by libssh2 1.11.1 (BL-680); Kerberos `rc4-hmac` (RFC 4757, BL-686). The BCL has neither. API and rules: BL-669's ADR.
- Vectors: RFC 6229 (RC4 keystream at offsets for 40- to 256-bit keys); RFC 2144 Appendix B (CAST-128 single-plaintext vectors for 128-, 80- and 40-bit keys, and the maintenance test, optionally marked `TestCategory=Integration` if slow).

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` pass the RFC 6229 vectors for at least the 40-, 128- and 256-bit keys at every listed offset, a 1536-byte discard case, and every RFC 2144 Appendix B.1 vector plus a CBC round trip.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
