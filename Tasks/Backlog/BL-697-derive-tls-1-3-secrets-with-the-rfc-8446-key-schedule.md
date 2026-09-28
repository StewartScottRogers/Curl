---
id: BL-697
title: Derive TLS 1.3 secrets with the RFC 8446 key schedule
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-696]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-697 — Derive TLS 1.3 secrets with the RFC 8446 key schedule

## Goal

`Curl.Tls.UnitLibrary` derives every TLS 1.3 secret and key from RFC 8446 section 7 (HKDF-Extract, HKDF-Expand-Label, Derive-Secret, early, handshake and master secrets, traffic keys and IVs, Finished keys, resumption master secret, PSK binder key, key update), for SHA-256 and SHA-384 suites, exposed so QUIC (BL-723) can reuse HKDF-Expand-Label with its own labels.

## Context

- Design: BL-695's ADR. BCL `HKDF`, `HMACSHA256`, `HMACSHA384`, `SHA256`, `SHA384`; the transcript hash accumulates handshake messages.
- Reference: RFC 8448 section 3 (simple 1-RTT handshake: every intermediate secret is printed), section 4 (resumed 0-RTT), and RFC 9001 Appendix A.1 (QUIC's use of HKDF-Expand-Label with `quic key`, `quic iv`, `quic hp`).

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` reproduce every secret, key and IV printed in RFC 8448 section 3 and the early secrets and binder of section 4 from their inputs, and RFC 9001 A.1's client and server initial keys through HKDF-Expand-Label.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
