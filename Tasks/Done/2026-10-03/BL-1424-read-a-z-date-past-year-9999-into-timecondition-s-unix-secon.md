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
completed: 2026-10-03
---
# BL-1424 — Read a -z date past year 9999 into TimeCondition's Unix seconds as curl does

## Goal

`-z`/`--time-cond` reads a date with a year past 9999 (for example `-z "Mon, 01 Jan 40000 00:00:00 GMT"`) into `TimeCondition.ValueUnixSeconds` exactly as curl 8.21.0 reads it.

## Context

- ADR-0410, decision 5. `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` `SetTimeCondition` builds `new TimeCondition(instant, kind)` from a `DateTimeOffset`, so such a date cannot be read today.
- curl reads `-z` dates with `curl_getdate`, the same parser that reads `Last-Modified`, and it read year 40000 there (measured 2026-10-03, BL-1409). Measure first with `Record-CurlExchange.ps1`: `curl -z "Mon, 01 Jan 40000 00:00:00 GMT" http://127.0.0.1:PORT/` (does it send `If-Modified-Since`, and is any warning printed?) and a year that overflows `time_t`.
- Depends on BL-1427 for `TimeCondition.ValueUnixSeconds`. The header text curl sends is BL-1426's.

## Acceptance criteria

- [x] `-z "Mon, 01 Jan 40000 00:00:00 GMT"` yields `TimeCondition.ValueUnixSeconds == 1200110860800` (test), and `-z -"…"` the same with `IfUnmodifiedSince`.
- [x] The measured curl behaviour (request bytes, stderr, exit code) is recorded in Notes, and any warning or refusal it shows is pinned by a test.
- [x] Existing `-z` date and file forms parse as today (existing tests pass).
- [x] `dotnet build` is clean, fast tests pass, `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 0 failing members; the option's meaning is unchanged, so `--ai-help` stays right (stated in Notes).

## Notes

- Measured 2026-10-03 with `Record-CurlExchange.ps1`, curl 8.21.0 Schannel (Windows): `-z "Mon, 01 Jan 40000 00:00:00 GMT" http://127.0.0.1:PORT/`, the same with a leading `-`, and `-z "Mon, 01 Jan 99999999 00:00:00 GMT"` each send no request, print `curl: (43) Invalid TIMEVALUE` on stderr and exit 43, with no parse warning. `31 Dec 3000 23:59:59` and `01 Jan 3001 00:00:00` send `If-Modified-Since` and exit 0. So curl reads these dates without complaint. The refusal comes from Windows' `gmtime` when the header is written, and `Curl.Protocol.Http.UnitLibrary/HttpTimeConditionLimit.cs` already handles it (ADR-0089, tested in `HttpProtocolHandlerTests.Conditions.cs`; it compares the clamped `Value`, which stays past its limit). Off Windows, curl's `gmtime` accepts year 40000 and sends the header (BL-1426's text). No year in `CurlDateParser`'s range (8 digits at most) overflows a 64-bit `time_t`, so there is no overflow case to read.
- `SetTimeCondition` now builds the condition with `TimeCondition.FromUnixSeconds` and keeps curl's Unix seconds whole instead of clamping to 9999-12-31 23:59:59. Past year 9999, `TimeCondition.Value` still reads as `DateTimeOffset.MaxValue`. The file form converts the file's modification time to Unix seconds. Tests: `Parse_TimeCondYear40000_IsThatYearsUnixSecondsWithNoWarning` (both directions), plus the year-9999 test, which now pins the Unix seconds.
- `--ai-help` stays right: the option's meaning and spelling are unchanged.
- `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 0 failing members. `dotnet build` clean, fast tests green.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. -z keeps a date past year 9999 as curl's Unix seconds (year 40000 = 1200110860800)
