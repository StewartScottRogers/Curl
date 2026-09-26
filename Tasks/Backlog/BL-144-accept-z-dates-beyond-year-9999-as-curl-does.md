---
id: BL-144
title: Accept -z dates beyond year 9999 as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-138]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Abstractions.UnitLibrary]
requirement: FR-009
created: 2026-09-26
completed:
---
# BL-144 — Accept -z dates beyond year 9999 as curl does

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

- [ ] `CurlDateParser.TryParse("1 Jan 099999999", ...)` no longer refuses the value, and a test
      in `Curl.Cli.UnitTests` pins what it yields, with the choice and why recorded in `Notes`.
- [ ] `curl -z "1 Jan 099999999"` prints no warning, as curl 8.21.0 does.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed from BL-138 as the one `curl_getdate` spelling range it does not support.

## Log

- 2026-09-26: Created.
