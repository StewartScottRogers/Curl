---
id: BL-4321
title: Extension: Privacy Pass VOPRF(P-384, SHA-384) client for token type 1
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.PrivacyPass.UnitLibrary/Tokens, Curl.Extension.PrivacyPass.UnitTests/Tokens]
requirement: none
created: 2026-10-29
completed:
---
# BL-4321 — Extension: Privacy Pass VOPRF(P-384, SHA-384) client for token type 1

## Goal

`Curl.Extension.PrivacyPass.UnitLibrary/Tokens` can issue a privately verifiable token (type `0x0001`, RFC 9578 §5): a hand-built P-384 group, hash-to-curve `P384_XMD:SHA-384_SSWU_RO_` (RFC 9380), and the VOPRF(P-384, SHA-384) client (RFC 9497 Blind, proof verification and Finalize), driven by `PrivacyPassIssuance` for a challenge of that type.

## Context

Split from BL-4315 (ADR-1110's "Not yet done"), which keeps only the wiring of type `0x0001` into `PrivacyPassTransferHook` (Options, held by BL-4316). Everything here is in `Tokens/`; base class library only (`System.Numerics.BigInteger`, `SHA384`).

- P-384 (SEC 2 / FIPS 186-5): affine or Jacobian arithmetic on `BigInteger`, SEC1 compressed encoding (49 bytes) and its decoding with an on-curve check; scalars mod the group order, 48 bytes big-endian.
- RFC 9380: `expand_message_xmd` with SHA-384, `hash_to_field` (L = 72), simplified SWU with Z = -12 (p ≡ 3 mod 4, so sqrt is x^((p+1)/4)), `hash_to_curve` = map(u0) + map(u1).
- RFC 9497 (mode 0x01, contextString `OPRFV1-\x01-P384-SHA384`): Blind (HashToGroup DST `HashToGroup-` || contextString), VerifyProof with ComputeComposites (`Seed-`, `Composite`, `Challenge`, HashToScalar DST `HashToScalar-` || contextString), Finalize (`Finalize`).
- RFC 9578 §5: token_key is the issuer's 49-byte compressed `pkS`; TokenRequest = type, truncated key ID, 49-byte blinded element; TokenResponse = 49-byte evaluated element || 96-byte proof; the token's authenticator is the 48-byte Finalize output.
- Tests: RFC 9497 Appendix A VOPRF P384-SHA384 vectors (blinded element from the fixed blind, proof verification, output) and RFC 9380 Appendix J.3.1 hash-to-curve vectors; a point-multiplication check against the BCL's `ECDiffieHellman` nistP384 key (d·G = Q) is a cheap cross-check. A test issuer in `Tokens/` can evaluate and prove with its own key for the issuance round trip.

## Acceptance criteria

- [ ] `Tokens/` holds the P-384 group, hash-to-curve and VOPRF client, and their unit tests in `Curl.Extension.PrivacyPass.UnitTests/Tokens` pass the RFC 9497 Appendix A P384-SHA384 VOPRF vectors and the RFC 9380 `P384_XMD:SHA-384_SSWU_RO_` vectors.
- [ ] `PrivacyPassIssuance` issues a type `0x0001` token for a challenge of that type, rejects a response whose proof does not verify (exit 8), and still issues type `0x0002` tokens as before.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Extension.PrivacyPass.UnitLibrary` reports every member inside the gates; `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-29: Created.
