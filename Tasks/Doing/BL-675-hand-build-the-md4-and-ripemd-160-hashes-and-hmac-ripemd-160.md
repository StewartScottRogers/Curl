---
id: BL-675
title: Hand-build the MD4 and RIPEMD-160 hashes and HMAC-RIPEMD-160
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-675 — Hand-build the MD4 and RIPEMD-160 hashes and HMAC-RIPEMD-160

## Goal

`Curl.Cryptography.UnitLibrary` computes MD4 (RFC 1320), RIPEMD-160 and HMAC-RIPEMD-160 (RFC 2286), matching their published vectors.

## Context

- Consumers: MD4 for the NTLM hash (MS-NLMP `NTOWFv1`, BL-684) and Kerberos `rc4-hmac` string-to-key (RFC 4757, BL-686); HMAC-RIPEMD-160 for SSH `hmac-ripemd160` and `hmac-ripemd160@openssh.com` (libssh2 1.11.1 offers them; BL-680). The BCL has neither. API and rules: BL-669's ADR.
- Vectors: RFC 1320 appendix A.5 (MD4 test suite); the RIPEMD-160 authors' page (Bosselaers) for the hash; RFC 2286 section 2 for HMAC-RIPEMD-160.
- Neither is a secret-keyed primitive on its own, so constant time applies only to the HMAC key handling.

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` pass all seven RFC 1320 A.5 vectors, the RIPEMD-160 reference vectors (including the million-`a` one, or mark it `TestCategory=Integration` if it is slow), and all RFC 2286 HMAC-RIPEMD-160 vectors, plus incremental hashing split across calls.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
