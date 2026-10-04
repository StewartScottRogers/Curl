# ADR-0412 — HTTP Last-Modified is read with curl's getdate

- **Status:** Accepted
- **Date:** 2026-10-03
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

ADR-0410 decision 2 has `HttpLastModified` read a `Last-Modified` year past 9999 into Unix
seconds. Until now it parsed with `DateTimeOffset.TryParseExact` in the three HTTP-date forms
ADR-0044 names, so `Mon, 01 Jan 40000 00:00:00 GMT` was an unknown time. curl 8.21.0 reads the
header with `Curl_getdate_capped` (`lib/http.c`), the same `parsedate` that `curl_getdate` and
`-z` use, and measured on 2026-10-03 it reads that header (BL-1428 Context).
`Curl.Protocol.Abstractions.UnitLibrary` already holds a port of that parser,
`CurlDateParser` (ADR-0074), which keeps years past 9999 as Unix seconds.

## Decision

`HttpLastModified.Find` reads the last `Last-Modified` value with `CurlDateParser.TryParse` and
returns Unix seconds; a value it refuses is an unknown time, as before. The handler sets
`TransferResult.SourceLastWriteUnixSeconds` from it, and `HttpDownloadConditions.IsMet`
compares it with `TimeCondition.ValueUnixSeconds`, as `Curl_meets_timecondition` compares
`time_t`s.

## Consequences

- The three HTTP-date forms read as before, and a year past 9999 now reaches `-R` and `-z`.
- Any other date curl's `parsedate` accepts (`1 Jan 2030`, a missing time or zone) is read
  too, as curl reads it; ADR-0044's "three forms" is narrowed to what curl actually does.
- One parser serves `-z`, cookie `Expires` and `Last-Modified`, so they cannot drift apart.
