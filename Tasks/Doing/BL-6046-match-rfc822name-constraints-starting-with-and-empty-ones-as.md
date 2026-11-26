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
completed:
---
# BL-6046 — Match rfc822Name constraints starting with @, and empty ones, as CryptoAPI does in the Schannel build

## Goal

Below a non-self-signed `--cacert` anchor, the Schannel build judges an rfc822Name constraint starting `@`, and an empty one, as curl's Schannel build does.

## Context

Found in BL-6011 (ADR-1430). CryptoAPI, measured with `X509Chain` on Windows: an excluded rfc822Name `@evil.test` or empty one breaks on a leaf rfc822Name `u@evil.test` (`0x00008000`). `CertificateNameConstraints.EmailMatches` compares `@evil.test` for equality and an empty constraint as a host, so neither matches. The OpenSSL build shares `EmailMatches`; its behaviour must not change unless measured. Measure permitted subtrees too.

## Acceptance criteria

- [ ] In the Schannel build an excluded rfc822Name constraint `@evil.test`, and an empty one, refuse `u@evil.test` with `0x00008000`, and permitted ones accept it.
- [ ] Unit tests in `Curl.Networking.UnitTests` pin both, and the OpenSSL build's answers are unchanged.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: a3ba62c7 3e79b7d6 3d836d5e.
