---
id: BL-1428
title: Read an HTTP Last-Modified past year 9999 into Unix seconds and compare -z in Unix seconds
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1427]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-011
created: 2026-10-03
completed: 2026-10-03
---
# BL-1428 — Read an HTTP Last-Modified past year 9999 into Unix seconds and compare -z in Unix seconds

## Goal

An HTTP `Last-Modified` with a year past 9999 is read into `TransferResult.SourceLastWriteUnixSeconds`, and `-z` compares the document time in Unix seconds, as curl 8.21.0's `curl_getdate` and `Curl_meets_timecondition` do.

## Context

- ADR-0410, decision 2. `Curl.Protocol.Http.UnitLibrary/HttpLastModified.cs` parses with `DateTimeOffset.TryParseExact`, so `Mon, 01 Jan 40000 00:00:00 GMT` is an unknown time today; `HttpDownloadConditions.IsMet` compares `DateTimeOffset`s.
- Measured 2026-10-03, curl 8.21.0 (mingw, Schannel), `--no-progress-meter -R -o <file>` against `Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT`: the time is read (the file is stamped, capped to 30827-12-31T23:59:59Z on Windows, exit 0), so the year is not limited to four digits.
- Depends on BL-1427 for `SourceLastWriteUnixSeconds` and `TimeCondition.ValueUnixSeconds`.

## Acceptance criteria

- [x] `HttpLastModified` reads `Mon, 01 Jan 40000 00:00:00 GMT` as Unix seconds 1200110860800, and the IMF-fixdate, RFC 850 and asctime forms keep reading as today (tests, including an existing in-range case and `garbage` as unknown).
- [x] An HTTP transfer under `-R` whose reply carries that header returns `SourceLastWriteUnixSeconds == 1200110860800` (test).
- [x] `-z` compares in Unix seconds: with `-z "1 Jan 2030"` that reply's body is delivered, and with `-z -"1 Jan 2030"` it is not (tests).
- [x] `dotnet build` is clean, fast tests pass, `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members.
- [x] No option changes, so `--ai-help` is unaffected (stated in Notes).

## Notes

- `HttpLastModified.Find` now reads with `CurlDateParser.TryParse` (curl's `parsedate`, which curl's `Curl_getdate_capped` uses for `Last-Modified`) and returns Unix seconds; the handler sets `SourceLastWriteUnixSeconds`; `HttpDownloadConditions.IsMet` compares seconds with `TimeCondition.ValueUnixSeconds`. Recorded in ADR-0412: it reads every date curl reads, not only the three HTTP-date forms.
- The first `Measure-CodeQuality` run flagged `HttpContentLength.TryParseItem` at 83% branch (the cached method-group delegate in `item.All(char.IsAsciiDigit)`); replaced with `AsSpan().ContainsAnyExceptInRange('0', '9')`, same behaviour, no hidden branch. Second run: 100% line, 100% branch, 0 failing members.
- No option changes, so `--ai-help` is unaffected.
- Tests: `Curl.Protocol.Http.UnitTests` 1802 passed, including the new `Find_YearPast9999_GivesItsUnixSeconds`, `ExecuteAsync_LastModifiedPastYear9999_SetsTheSourceTimeInUnixSeconds` and `ExecuteAsync_LastModifiedPastYear9999_ComparesTheConditionInUnixSeconds` (both directions); the full fast suite is green.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. An HTTP Last-Modified past year 9999 reaches -R as Unix seconds and -z compares in Unix seconds
