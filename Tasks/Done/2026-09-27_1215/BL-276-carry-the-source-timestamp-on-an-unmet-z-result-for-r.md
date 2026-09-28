---
id: BL-276
title: Carry the source timestamp on an unmet -z result for -R
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Documentation/Product/Requirements.md]
requirement: FR-011
created: 2026-09-26
completed: 2026-09-26
---
# BL-276 — Carry the source timestamp on an unmet -z result for -R

## Goal

`FileProtocolHandler` returns `TransferResult.TimeConditionNotMet(timestamp)` with the source's
whole-second modification time for an unmet `-z`, or FR-011 stops claiming `-R` applies then,
whichever real curl 8.21.0 shows.

## Context

- FR-011 (`Documentation/Product/Requirements.md`) says `-R` is applied "even when no body
  was written (an unmet `-z`)".
- `Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs` `DownloadFromAsync` returns
  `TransferResult.TimeConditionNotMet()` with no timestamp, so `Curl.Console` has nothing to
  apply. Found while working BL-142.
- An unmet `-z` creates no `-o` file (BL-136), so only an existing `-o` file could be stamped.
  Measure first: `curl -R -z "1 Jan 2030" -o existing.txt file:///...` on curl 8.21.0 -
  does `existing.txt`'s modification time change?

## Acceptance criteria

- [x] The measured curl 8.21.0 behaviour for `-R -z <future date> -o <existing file>` is
      recorded under Notes.
- [x] Either a `FileProtocolHandlerTests` test shows an unmet `-z` result carries the
      truncated source timestamp, or FR-011 no longer claims `-R` applies to an unmet `-z`.

## Notes

- Measured 2026-09-26 on curl 8.21.0 (x86_64-w64-mingw32, Schannel), source modified
  2020-03-04 05:06:07.5 local: `curl -R -z "1 Jan 2030" -o existing.txt file:///.../src.txt`
  exits 0, leaves `existing.txt`'s content ("old") untouched and sets its modification time
  to 2020-03-04 05:06:07.000 - the source's time truncated to whole seconds. With no existing
  `-o` file it exits 0, creates nothing and prints `Warning: Failed to set filetime
  1583323567 on outfile: CreateFile failed:` and `Warning: GetLastError 0x00000002`; those
  warning lines are BL-139's scope. A met `-z` stamps the new file the same way.
- The Context was out of date: `ExecuteAsync` already stamps every successful result,
  and `TransferResult.TimeConditionNotMet()` is a success (exit 0), so the unmet result
  already carried the truncated timestamp. No production behaviour changed; added
  `ExecuteAsync_UnmetTimeCondition_ReportsTheSourceTimestampTruncatedToSeconds`, made the
  handler comment say an unmet `-z` is included, and put the measured behaviour in FR-011.
- No ADR: the behaviour is measured upstream behaviour, not a choice.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. An unmet -z result is proven to carry the source's whole-second timestamp for -R, matching curl 8.21.0; FR-011 records the measurement
