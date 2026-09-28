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
completed: 2026-09-28
---
# BL-697 — Derive TLS 1.3 secrets with the RFC 8446 key schedule

## Goal

`Curl.Tls.UnitLibrary` derives every TLS 1.3 secret and key from RFC 8446 section 7 (HKDF-Extract, HKDF-Expand-Label, Derive-Secret, early, handshake and master secrets, traffic keys and IVs, Finished keys, resumption master secret, PSK binder key, key update), for SHA-256 and SHA-384 suites, exposed so QUIC (BL-723) can reuse HKDF-Expand-Label with its own labels.

## Context

- Design: BL-695's ADR. BCL `HKDF`, `HMACSHA256`, `HMACSHA384`, `SHA256`, `SHA384`; the transcript hash accumulates handshake messages.
- Reference: RFC 8448 section 3 (simple 1-RTT handshake: every intermediate secret is printed), section 4 (resumed 0-RTT), and RFC 9001 Appendix A.1 (QUIC's use of HKDF-Expand-Label with `quic key`, `quic iv`, `quic hp`).

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` reproduce every secret, key and IV printed in RFC 8448 section 3 and the early secrets and binder of section 4 from their inputs, and RFC 9001 A.1's client and server initial keys through HKDF-Expand-Label.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built as ADR-0140's table names it: public `HkdfLabel` (`Encode`, `Expand`),
  `Tls13KeySchedule` and `TranscriptHash`, plus the `Tls13TrafficKeys` record. No new
  design decision, so no new ADR.
- Choice: the key schedule is stateless - each method takes its secret and transcript
  hash and returns the next - so the I/O-free handshake (BL-699) owns the state and every
  step is testable against one line of an RFC 8448 trace. A stateful schedule would hide
  which transcript point each secret binds.
- Choice: hashes are `HashAlgorithmName`, restricted to SHA-256 and SHA-384 (anything
  else is an `ArgumentException`); the traffic key length is a parameter (16 or 32),
  since the cipher suite table belongs to the handshake task.
- Test vectors were copied from the RFC text fetched with curl (rfc-editor.org). The
  transcript hashes are computed from the existing `Rfc8448Messages` bytes, so the
  traces also check `TranscriptHash`. RFC 8448 has no SHA-384 trace: SHA-384 is checked
  against the BCL's HMAC and HKDF directly, as are `traffic upd` and `ext binder`, which
  no trace prints.
- `TranscriptHash.ReplaceWithMessageHash` (HelloRetryRequest) is here because it is part
  of the transcript hash; BL-699 drives it.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Tls13KeySchedule, HkdfLabel and TranscriptHash derive every RFC 8446 section 7 secret for SHA-256 and SHA-384, pinned to RFC 8448 sections 3-4 and RFC 9001 A.1
