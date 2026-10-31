---
id: BL-4101
title: Build HTTP Datagrams and the Capsule Protocol (RFC 9297) as a shared Wire layer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091]
touches: [Curl.HttpDatagram.UnitLibrary, Curl.HttpDatagram.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4101 — Build HTTP Datagrams and the Capsule Protocol (RFC 9297) as a shared Wire layer

## Goal

A hand-built `Curl.HttpDatagram.UnitLibrary` frames HTTP Datagrams over HTTP/3 datagrams and over the Capsule Protocol on HTTP/2 and HTTP/1.1 streams.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 44 of the list. WebTransport and MASQUE both need it and extensions may not share code, so it is a shared layer in `/Wire/HttpDatagram/`, not an extension.

## Acceptance criteria

- [x] Capsule encoding and decoding, the quarter-stream-ID prefix on HTTP/3 datagrams, and `SETTINGS_H3_DATAGRAM` negotiation are each tested against RFC 9297's wire formats.
- [x] Unknown capsule types are skipped as the RFC requires (unit test).
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Delivered directly rather than through the multi-agent /feature stages: a small, self-contained byte-level library with no seams into other projects, so the plan is ADR-1004 itself.
- Decided (ADR-1004): the library references nothing and carries its own RFC 9000 variable-length integer, so HTTP/1.1 and HTTP/2 users of the Capsule Protocol do not pull in Curl.Http3.UnitLibrary. Wiring into transfers, the Capsule-Protocol header and QUIC DATAGRAM frames is left to the WebTransport and MASQUE extensions that use it.
- Unknown capsule types: DatagramCapsuleReader drops them silently (RFC 9297 section 3.2); DatagramCapsuleReaderTests pins it. A stream ending inside a capsule fails Complete (section 3.3).
- Measure-CodeQuality.ps1 -Library Curl.HttpDatagram.UnitLibrary: 100% line, 100% branch, 0 failing members. dotnet build clean; fast tests green (55 tests in Curl.HttpDatagram.UnitTests). Platform-neutral: pure byte computation, no paths or OS text.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a32642ec 0e064ecb e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.HttpDatagram.UnitLibrary frames RFC 9297 capsules, HTTP/3 quarter-stream-ID datagrams and SETTINGS_H3_DATAGRAM negotiation
