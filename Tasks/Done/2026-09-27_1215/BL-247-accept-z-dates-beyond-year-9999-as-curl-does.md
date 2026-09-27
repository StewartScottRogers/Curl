---
id: BL-247
title: Accept -z dates beyond year 9999 as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-138]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Documentation/Planning/Decisions]
requirement: FR-009
created: 2026-09-26
completed: 2026-09-27
---
# BL-247 — Accept -z dates beyond year 9999 as curl does

## Goal

A `-z` date whose year lies beyond 9999 (or before year 1), which curl 8.21.0 accepts, is
accepted with curl's meaning instead of being reported as not a date.

## Context

- BL-138's `CurlDateParser` ports libcurl 8.21.0's `parsedate`. curl computes the instant in a
  64-bit `time_t`; `TimeCondition.Value` (`Curl.Protocol.Abstractions.UnitLibrary`) is a
  `DateTimeOffset`, which stops at 9999-12-31, so the parser refuses such dates.
- Measured on curl 8.21.0 (Windows, 2026-09-26): `curl -z "1 Jan 099999999" file:///...` prints
  no warning (accepted); this port prints the illegal-date warning.
- For `file://` any such date compares the same as the largest representable one, so clamping
  may be enough; decide (and record an ADR if the contract changes) whether to clamp in the
  parser or widen `TimeCondition`.

## Acceptance criteria

- [x] `CurlDateParser.TryParse("1 Jan 099999999", ...)` no longer refuses the value, and a test
      in `Curl.Cli.UnitTests` pins what it yields, with the choice and why recorded in `Notes`.
- [x] `curl -z "1 Jan 099999999"` prints no warning, as curl 8.21.0 does.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed from BL-138 as the one `curl_getdate` spelling range it does not support.
- Decision (ADR-0073): clamp in the parser, not widen `TimeCondition`. An instant after the last
  whole second a `DateTimeOffset` holds reads as 9999-12-31 23:59:59 UTC; for `file://` and any
  `Last-Modified` comparison that compares exactly as curl's 64-bit instant does, and no shared
  contract changes. Pinned by `CurlDateParserTests.TryParse_InstantAfterYear9999_IsTheLastWholeSecondOfYear9999`
  (`1 Jan 099999999`, `31 Dec 9999 23:00 -1400`) and
  `CommandLineTimeConditionOptionTests.Parse_TimeCondDateAfterYear9999_IsIfModifiedSinceTheLastSecondOfYear9999WithNoWarning`.
- Measured curl 8.21.0 (Windows) on 2026-09-27 against a file written that day: `-z "1 Jan 099999999"`
  writes nothing, `-z "-1 Jan 099999999"` writes the file, no warning either way; this port now does
  the same (checked with `dotnet run --project Curl.Console`).
- The low end is not clamped: `00000101` (year 0) is not a date to curl (both directions transfer;
  its 1583 floor, BL-256), so it stays refused (`TryParse_InstantBeforeYear1_IsRefused`).
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0073 and its index row; no task in
  Doing names it.
- Follow-up: BL-381, the HTTP `If-Modified-Since` header for such a date (clamped header vs curl's,
  unmeasured).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -z dates after the year 9999 are accepted as curl 8.21.0 accepts them, read as 9999-12-31 23:59:59 UTC (ADR-0073), with no warning
