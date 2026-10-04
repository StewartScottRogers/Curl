---
id: BL-1426
title: Send If-Modified-Since for a -z date past year 9999 in curl's measured form
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1428, BL-1424]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-011
created: 2026-10-03
completed:
---
# BL-1426 — Send If-Modified-Since for a -z date past year 9999 in curl's measured form

## Goal

An HTTP request under a `-z` date past year 9999 sends `If-Modified-Since`/`If-Unmodified-Since` (or none) byte for byte as curl 8.21.0 does.

## Context

- ADR-0410, decision 2. curl formats the condition's `time_t` with its own `Curl_gmtime` and `%s, %02d %s %4d %02d:%02d:%02d GMT`; how a five-digit year comes out must be measured, not guessed.
- Measure with `Record-CurlExchange.ps1`: `curl -z "Mon, 01 Jan 40000 00:00:00 GMT" http://127.0.0.1:PORT/` and the `-z -` form; pin the request bytes. BL-1424 records the Cli side of the same measurement.
- The header is built in `Curl.Protocol.Http.UnitLibrary` from `TimeCondition`; build it from `ValueUnixSeconds` (BL-1427) when `Value` is out of range.
- Depends on BL-1428 (the same library's Unix-seconds comparison) and BL-1424 (the Cli reads such a date).

## Acceptance criteria

- [ ] A request with `TimeCondition.ValueUnixSeconds == 1200110860800` sends the header line curl 8.21.0 sent in the measurement, recorded in Notes (test), for both condition kinds.
- [ ] In-range `-z` requests send the same bytes as today (existing tests pass).
- [ ] `dotnet build` is clean, fast tests pass, `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members; no option changes, so `--ai-help` is unaffected.

## Notes

## Log

- 2026-10-03: Created.
