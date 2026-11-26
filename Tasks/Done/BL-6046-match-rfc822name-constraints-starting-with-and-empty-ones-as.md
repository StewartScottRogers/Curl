---
id: BL-6046
title: Match rfc822Name constraints starting with @, and empty ones, as CryptoAPI does in the Schannel build
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6046 — Match rfc822Name constraints starting with @, and empty ones, as CryptoAPI does in the Schannel build

## Goal

Below a non-self-signed `--cacert` anchor, the Schannel build judges an rfc822Name constraint starting `@`, and an empty one, as curl's Schannel build does.

## Context

Found in BL-6011 (ADR-1430). CryptoAPI, measured with `X509Chain` on Windows: an excluded rfc822Name `@evil.test` or empty one breaks on a leaf rfc822Name `u@evil.test` (`0x00008000`). `CertificateNameConstraints.EmailMatches` compares `@evil.test` for equality and an empty constraint as a host, so neither matches. The OpenSSL build shares `EmailMatches`; its behaviour must not change unless measured. Measure permitted subtrees too.

## Acceptance criteria

- [x] In the Schannel build an excluded rfc822Name constraint `@evil.test`, and an empty one, refuse `u@evil.test` with `0x00008000`, and permitted ones accept it.
- [x] Unit tests in `Curl.Networking.UnitTests` pin both, and the OpenSSL build's answers are unchanged.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured CryptoAPI with `X509Chain`, excluded and permitted subtrees (table in ADR-1437): empty and `@`-prefixed constraints match the end of the name, ignoring case; a leading-dot constraint holding `@` stays an equality, as before.
- `CertificateNameConstraints` now carries each build's rfc822Name comparison with the names it reads. The matchers are held in static fields, because passing the method groups added branches that failed the complexity gate.
- Empty-constraint tests are excluded on macOS: its certificate parser was not measured with one. The rest are pure matching over a built chain, as the existing constraint tests are.
- Fast tests: all 114 test projects passed; `Curl.Networking.UnitTests` 4813 passed, 33 skipped, rerun after the last edit. `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.
- OpenSSL build left unchanged; BL-6054 filed to measure it.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: a3ba62c7 3e79b7d6 3d836d5e.
- 2026-11-26: Doing -> Done. The Schannel build matches empty and @-prefixed rfc822Name constraints as CryptoAPI does
