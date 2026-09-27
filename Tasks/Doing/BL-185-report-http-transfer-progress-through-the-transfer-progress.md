---
id: BL-185
title: Report HTTP transfer progress through the transfer progress sink
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-173, BL-134]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-185 — Report HTTP transfer progress through the transfer progress sink

## Goal

The HTTP handler reports transfer start, totals and counters through the progress sink BL-134 adds, per BL-133's ADR.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H17. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-133 (ADR) and BL-134 (contract) define the sink; BL-129 does the same for file://.

## Acceptance criteria

- [ ] Tests show the sink receives start, expected download total (Content-Length or unknown), and running byte counts for a scripted exchange, per BL-134's contract.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H17 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
