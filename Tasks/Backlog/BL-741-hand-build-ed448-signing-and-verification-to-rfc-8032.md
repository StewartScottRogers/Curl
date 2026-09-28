---
id: BL-741
title: Hand-build Ed448 signing and verification to RFC 8032
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-740, BL-743]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-741 — Hand-build Ed448 signing and verification to RFC 8032

## Goal

`Curl.Cryptography.UnitLibrary` derives Ed448 public keys, signs and verifies (PureEdDSA Ed448 with an optional context, RFC 8032 section 5.2) in constant time for the secret parts, reusing X448's field type (BL-740) and the hand-built SHAKE256 (BL-743), because the BCL's `Shake256` is not supported on macOS.

## Context

- ADR-0118 (BL-669): the BCL has no Ed448 on any platform and no SHAKE256 on macOS (Microsoft Learn, "Cross-platform cryptography in .NET", checked 2026-09-28).
- Consumers: TLS `ed448` signature scheme for server certificates and `CertificateVerify` (BL-699, BL-709), which OpenSSL offers by default; client certificates with Ed448 keys for `--cert`/`--key`.
- Vectors: RFC 8032 section 7.4 (Ed448 test vectors: blank, 1 octet, 1 octet with context, 11, 12, 13, 64, 256 and 1023 octets).
- Verification rejects a non-canonical S or point encoding as RFC 8032 section 5.2.7 requires.

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` pass every RFC 8032 section 7.4 vector for key derivation, signing and verification.
- [ ] Verification returns `false` for a flipped message bit, a flipped signature bit and an S not below the group order.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
