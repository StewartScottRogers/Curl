---
id: BL-4100
title: Build HPKE (RFC 9180) in Curl.Cryptography
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary/Hpke, Curl.Cryptography.UnitTests/Hpke]
requirement: none
created: 2026-10-29
completed:
---
# BL-4100 — Build HPKE (RFC 9180) in Curl.Cryptography

## Goal

Curl.Cryptography offers HPKE base mode with DHKEM(X25519, HKDF-SHA256), HKDF-SHA256 and AES-128-GCM / ChaCha20-Poly1305, for Oblivious HTTP and Oblivious DoH.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Shared by two extensions, so it lives in the shared cryptography layer. Reuse the X25519 and ChaCha20-Poly1305 already hand-built for SSH.

## Acceptance criteria

- [ ] Every RFC 9180 Appendix A test vector for those suites passes (seal and open, exporter).
- [ ] Comparisons of secrets are constant-time (reviewed against `CryptographicOperations.FixedTimeEquals`).
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes



## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 95196793 a1d6e106 e35aff77 3d836d5e.
