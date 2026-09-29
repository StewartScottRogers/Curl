---
id: BL-704
title: Run the TLS-SRP key exchange to RFC 5054 in the hand-built TLS client
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-703]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-704 — Run the TLS-SRP key exchange to RFC 5054 in the hand-built TLS client

## Goal

The hand-built TLS 1.2 client authenticates with a user name and password through the SRP key exchange (RFC 5054: the `srp` extension, SRP ServerKeyExchange with N, g, s, B, the client's A and premaster secret, the RFC 5054 groups, and the SRP suites BL-695's ADR lists), so `--tlsuser`, `--tlspassword` and `--tlsauthtype SRP` work (BL-712).

## Context

- Design: BL-695's ADR. Builds on BL-703. `System.Numerics.BigInteger` for the modular arithmetic, `SHA1` for SRP-6a's hashes (RFC 5054 section 2.5), and a check that the server's group is one of Appendix A's.
- curl's `--tlsauthtype`: "Set the TLS authentication type. Several TLS authentication types are supported: SRP." (https://curl.se/docs/manpage.html, checked 2026-09-28).
- Reference: RFC 5054 Appendix B (the SRP test vectors: I, P, s, k, x, v, a, b, A, B, u, premaster secret).

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` reproduce every RFC 5054 Appendix B value, complete an SRP handshake with an in-memory server for each SRP suite, and fail a wrong password (the server's Finished does not verify), a group not in Appendix A and `B mod N == 0` with typed alerts.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
