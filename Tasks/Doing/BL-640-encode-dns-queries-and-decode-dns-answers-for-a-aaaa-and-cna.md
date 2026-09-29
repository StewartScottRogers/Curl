---
id: BL-640
title: Encode DNS queries and decode DNS answers for A, AAAA and CNAME records
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-639]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-640 — Encode DNS queries and decode DNS answers for A, AAAA and CNAME records

## Goal

A DNS message codec in `Curl.Networking.UnitLibrary` encodes the query curl 8.21.0 sends for DoH (ID 0, RD set, one question, A or AAAA) byte for byte, and decodes answers (name compression, CNAME chains, TTLs, RCODE errors, truncated or malformed messages) into addresses or a typed failure.

## Context

- Conformance audit 2026-09-28, row 27. Design: BL-639's ADR (which records the query bytes curl sent).
- RFC 1035 (message format and compression), RFC 3596 (AAAA), RFC 8484 section 4.1 (ID 0). Pure code: bytes in, bytes out.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` reproduce the query bytes BL-639 recorded, decode answers with compression pointers, a CNAME chain, a pointer loop (rejected), NXDOMAIN and SERVFAIL, and a truncated message.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
