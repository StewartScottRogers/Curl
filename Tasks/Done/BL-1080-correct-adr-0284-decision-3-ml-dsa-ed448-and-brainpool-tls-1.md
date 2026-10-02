---
id: BL-1080
title: Correct ADR-0284 decision 3: ML-DSA, ed448 and brainpool TLS 1.3 schemes are offered, not dropped
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0284-curves-and-sigalgs-read-openssl-3-5-lists-and-fail-as-the-applying-build.md]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1080 â€” Correct ADR-0284 decision 3: ML-DSA, ed448 and brainpool TLS 1.3 schemes are offered, not dropped

## Goal

ADR-0284 states what the hand-built client does with ML-DSA, ed448 and brainpool TLS 1.3 schemes now: it offers and checks them, as OpenSSL 3.5's curl does.

## Context

- ADR-0284 decision 3 (around line 80) and its consequences (around line 97-101) say schemes the client cannot check (ML-DSA, ed448, brainpool TLS 1.3 ECDSA) are dropped and that BL-1047 covers them.
- BL-940 made TlsSignatureScheme check all seven, so ClientHelloProfileMapping.CheckableSignatureAlgorithms keeps them; BL-1047 pinned it (HandBuiltTlsProviderTests WithSigalgs rows, OpenSslCertificateVerifyKnownAnswerTests).

## Acceptance criteria

- [x] ADR-0284 no longer says ML-DSA, ed448 or brainpool TLS 1.3 schemes are dropped; it names BL-940 and BL-1047 as where they became offered and checked.

## Notes

- Docs only, no `.cs` changed, so no build or test run was needed. Checked against the code first: `ClientHelloProfileMapping.CheckableSignatureAlgorithms` keeps every scheme of both profiles (`CheckableSignatureAlgorithms_KeepEverySchemeOfBothProfiles`). Decision 3 now says a scheme the client cannot check would be dropped, but every scheme `--sigalgs` names is checkable since BL-940 (pinned by BL-1047); the Consequences bullet no longer lists signature schemes as a byte difference.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. ADR-0284 says ML-DSA, ed448 and brainpool TLS 1.3 schemes are offered and checked (BL-940, BL-1047)
