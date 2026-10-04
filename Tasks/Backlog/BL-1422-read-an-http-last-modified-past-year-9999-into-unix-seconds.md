---
id: BL-1422
title: Read an HTTP Last-Modified past year 9999 into Unix seconds and compare -z in Unix seconds
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1421]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-011
created: 2026-10-03
completed:
---
# BL-1422 — Read an HTTP Last-Modified past year 9999 into Unix seconds and compare -z in Unix seconds

## Goal

An HTTP `Last-Modified` with a year past 9999 is read into `TransferResult.SourceLastWriteUnixSeconds`, and `-z` compares the document time in Unix seconds, as curl 8.21.0's `curl_getdate` and `Curl_meets_timecondition` do.

## Context

- ADR-0410, decision 2. `Curl.Protocol.Http.UnitLibrary/HttpLastModified.cs` parses with `DateTimeOffset.TryParseExact`, so `Mon, 01 Jan 40000 00:00:00 GMT` is an unknown time today; `HttpDownloadConditions.IsMet` compares `DateTimeOffset`s.
- Measured 2026-10-03, curl 8.21.0 (mingw, Schannel), `--no-progress-meter -R -o <file>` against `Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT`: the time is read (the file is stamped, capped to 30827-12-31T23:59:59Z on Windows, exit 0), so the year is not limited to four digits.
- Depends on BL-1421 for `SourceLastWriteUnixSeconds` and `TimeCondition.ValueUnixSeconds`.

## Acceptance criteria

- [ ] `HttpLastModified` reads `Mon, 01 Jan 40000 00:00:00 GMT` as Unix seconds 1200110860800, and the IMF-fixdate, RFC 850 and asctime forms keep reading as today (tests, including an existing in-range case and `garbage` as unknown).
- [ ] An HTTP transfer under `-R` whose reply carries that header returns `SourceLastWriteUnixSeconds == 1200110860800` (test).
- [ ] `-z` compares in Unix seconds: with `-z "1 Jan 2030"` that reply's body is delivered, and with `-z -"1 Jan 2030"` it is not (tests).
- [ ] `dotnet build` is clean, fast tests pass, `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members.
- [ ] No option changes, so `--ai-help` is unaffected (stated in Notes).

## Notes

## Log

- 2026-10-03: Created.
