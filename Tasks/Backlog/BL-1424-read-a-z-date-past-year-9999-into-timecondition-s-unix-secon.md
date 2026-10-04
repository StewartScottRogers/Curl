---
id: BL-1424
title: Read a -z date past year 9999 into TimeCondition's Unix seconds as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1427]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: FR-011
created: 2026-10-03
completed:
---
# BL-1424 — Read a -z date past year 9999 into TimeCondition's Unix seconds as curl does

## Goal

`-z`/`--time-cond` reads a date with a year past 9999 (for example `-z "Mon, 01 Jan 40000 00:00:00 GMT"`) into `TimeCondition.ValueUnixSeconds` exactly as curl 8.21.0 reads it.

## Context

- ADR-0410, decision 5. `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` `SetTimeCondition` builds `new TimeCondition(instant, kind)` from a `DateTimeOffset`, so such a date cannot be read today.
- curl reads `-z` dates with `curl_getdate`, the same parser that reads `Last-Modified`, and it read year 40000 there (measured 2026-10-03, BL-1409). Measure first with `Record-CurlExchange.ps1`: `curl -z "Mon, 01 Jan 40000 00:00:00 GMT" http://127.0.0.1:PORT/` (does it send `If-Modified-Since`, and is any warning printed?) and a year that overflows `time_t`.
- Depends on BL-1427 for `TimeCondition.ValueUnixSeconds`. The header text curl sends is BL-1426's.

## Acceptance criteria

- [ ] `-z "Mon, 01 Jan 40000 00:00:00 GMT"` yields `TimeCondition.ValueUnixSeconds == 1200110860800` (test), and `-z -"…"` the same with `IfUnmodifiedSince`.
- [ ] The measured curl behaviour (request bytes, stderr, exit code) is recorded in Notes, and any warning or refusal it shows is pinned by a test.
- [ ] Existing `-z` date and file forms parse as today (existing tests pass).
- [ ] `dotnet build` is clean, fast tests pass, `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 0 failing members; the option's meaning is unchanged, so `--ai-help` stays right (stated in Notes).

## Notes

## Log

- 2026-10-03: Created.
