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
completed: 2026-10-03
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

- [x] A request with `TimeCondition.ValueUnixSeconds == 1200110860800` sends the header line curl 8.21.0 sent in the measurement, recorded in Notes (test), for both condition kinds.
- [x] In-range `-z` requests send the same bytes as today (existing tests pass).
- [x] `dotnet build` is clean, fast tests pass, `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members; no option changes, so `--ai-help` is unaffected.

## Notes
- Measured 2026-10-03 against curl 8.18.0's OpenSSL build on Linux (WSL Ubuntu 26.04; `nc -l` as the server, because the Windows build never sends this header): `-z "Mon, 01 Jan 40000 00:00:00 GMT"` sends `If-Modified-Since: Sat, 01 Jan 40000 00:00:00 GMT`, the `-` form sends `If-Unmodified-Since: Sat, 01 Jan 40000 00:00:00 GMT`, and `-z "Mon, 01 Jan 99999999 00:00:00 GMT"` sends `If-Modified-Since: Fri, 01 Jan 99999999 00:00:00 GMT`; each exits 0. curl writes the whole year and works out the weekday from the date, ignoring the one in the `-z` text. curl 8.21.0 builds this header with the same `Curl_gmtime` and format string, so the 8.18.0 bytes are used for 8.21.0. On Windows curl 8.21.0 still exits 43 before sending anything (BL-1424 Notes), and `HttpTimeConditionLimit` already does the same.
- New `HttpConditionDate.Format`: RFC 1123 from `Value` in range (the same bytes as before), and for times past 9999 a civil date from `ValueUnixSeconds` using 400-year eras. Tests: `HttpConditionDateTests` (year 40000, 10000-01-01, 10000-03-01 after a leap day, the last second of 9999) and `ExecuteAsync_TimeConditionInYear40000_FailsWith43OnWindowsAndSendsTheFiveDigitYearElsewhere` (both kinds).
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members. `dotnet build` is clean and the fast tests pass. No option changed, so `--ai-help` stays right.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. A -z date past year 9999 sends If-Modified-Since/If-Unmodified-Since with the whole year, as curl does off Windows
