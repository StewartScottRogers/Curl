---
id: BL-703
title: Run the TLS 1.2 client handshake in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-702, BL-698, BL-671]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-703 — Run the TLS 1.2 client handshake in the hand-built TLS client

## Goal

The hand-built client runs a TLS 1.2 (and 1.1/1.0 when the version range allows) client handshake: ECDHE (X25519 and NIST curves) and RSA key exchange, the suites BL-695's ADR lists, `renegotiation_info` (RFC 5746), extended master secret, ALPN, SNI, `status_request`, session-ID and ticket resumption (RFC 5077), an optional client certificate, and the chain handed to the verifier.

## Context

- Design: BL-695's ADR. Builds on BL-702 (records and PRF), BL-698 (the shared ClientHello encoder and extension codecs) and X25519 (BL-671).
- References: RFC 5246 section 7, RFC 8422 (ECDHE and point formats), RFC 5746, RFC 5077, RFC 6066.

## Acceptance criteria

- [ ] An in-memory TLS 1.2 server in `Curl.Tls.UnitTests` completes handshakes for each suite family and key exchange, resumes by session ID and by ticket, and the client rejects a bad ServerKeyExchange signature, a bad Finished and a missing `renegotiation_info` with the typed alert; a TLS 1.0 and a 1.1 handshake complete when the range allows them and fail with `protocol_version` otherwise.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
