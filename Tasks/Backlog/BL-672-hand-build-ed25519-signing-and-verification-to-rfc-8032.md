---
id: BL-672
title: Hand-build Ed25519 signing and verification to RFC 8032
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-671]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-672 — Hand-build Ed25519 signing and verification to RFC 8032

## Goal

`Curl.Cryptography.UnitLibrary` derives Ed25519 public keys from 32-byte seeds, signs and verifies exactly as RFC 8032 section 5.1 specifies, reusing BL-671's field arithmetic.

## Context

- Consumers: SSH `ssh-ed25519` and `ssh-ed25519-cert-v01@openssh.com` host keys (BL-678, BL-566), Ed25519 user keys (BL-681), TLS 1.3 `ed25519` CertificateVerify and certificate signatures (BL-699). API and rules: BL-669's ADR.
- RFC 8032 section 5.1 (Ed25519: encoding, decoding with the canonical checks, SHA-512 from the BCL), section 7.1 (test vectors TEST 1, 2, 3, 1024 and SHA(abc)). Verification rejects a non-canonical `S` (S >= L) and a point that fails to decode.
- Signing is constant-time in the secret scalar; verification need not be.

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` reproduce every RFC 8032 section 7.1 Ed25519 vector (public key from secret, signature bytes, verification true), and verification is false for a flipped message bit, a flipped signature bit, `S >= L` and an undecodable public key.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
