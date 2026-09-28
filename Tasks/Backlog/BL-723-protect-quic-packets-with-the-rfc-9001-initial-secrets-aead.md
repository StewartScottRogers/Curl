---
id: BL-723
title: Protect QUIC packets with the RFC 9001 initial secrets, AEAD and header protection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-722, BL-673, BL-697]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-723 — Protect QUIC packets with the RFC 9001 initial secrets, AEAD and header protection

## Goal

`Curl.Quic.UnitLibrary` derives initial secrets from the destination connection ID (RFC 9001 section 5.2), protects and unprotects packet payloads with AES-128-GCM, AES-256-GCM and ChaCha20-Poly1305, applies and removes header protection with AES-ECB and ChaCha20 (section 5.4), handles key update (section 6), and builds Retry integrity tags (section 5.8), matching RFC 9001 Appendix A byte for byte.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-722 (packets). HKDF-Expand-Label with the `quic` labels from `Curl.Tls.UnitLibrary` (BL-697); raw ChaCha20 and the AEAD from `Curl.Cryptography.UnitLibrary` (BL-673); AES-ECB and `AesGcm` from the BCL (or the fallback BL-669's ADR names). Add both references here (allowed by BL-667's ADR).
- References: RFC 9001 Appendix A.1 (keys), A.2 (client Initial), A.3 (server Initial), A.4 (Retry), A.5 (ChaCha20-Poly1305 short header packet).

## Acceptance criteria

- [ ] `Curl.Quic.UnitTests` reproduce every key, IV and header-protection key of A.1, the protected packets of A.2, A.3 and A.5 from their plaintext, the Retry tag of A.4, and remove protection from each back to the plaintext; a tampered packet fails AEAD with the typed failure and is dropped, not fatal, as RFC 9001 requires.
- [ ] A key update round trip (phase bit flip, next-generation keys) is covered.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
