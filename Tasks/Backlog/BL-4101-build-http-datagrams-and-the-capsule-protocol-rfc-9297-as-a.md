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
completed:
---
# BL-4101 — Build HTTP Datagrams and the Capsule Protocol (RFC 9297) as a shared Wire layer

## Goal

A hand-built `Curl.HttpDatagram.UnitLibrary` frames HTTP Datagrams over HTTP/3 datagrams and over the Capsule Protocol on HTTP/2 and HTTP/1.1 streams.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 44 of the list. WebTransport and MASQUE both need it and extensions may not share code, so it is a shared layer in `/Wire/HttpDatagram/`, not an extension.

## Acceptance criteria

- [ ] Capsule encoding and decoding, the quarter-stream-ID prefix on HTTP/3 datagrams, and `SETTINGS_H3_DATAGRAM` negotiation are each tested against RFC 9297's wire formats.
- [ ] Unknown capsule types are skipped as the RFC requires (unit test).
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes



## Log

- 2026-10-29: Created.
