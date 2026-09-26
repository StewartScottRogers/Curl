---
id: BL-274
title: Carry the source timestamp on an unmet -z result for -R
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Documentation/Product/Requirements.md]
requirement: FR-011
created: 2026-09-26
completed:
---
# BL-274 — Carry the source timestamp on an unmet -z result for -R

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

- [ ] The measured curl 8.21.0 behaviour for `-R -z <future date> -o <existing file>` is
      recorded under Notes.
- [ ] Either a `FileProtocolHandlerTests` test shows an unmet `-z` result carries the
      truncated source timestamp, or FR-011 no longer claims `-R` applies to an unmet `-z`.

## Notes

## Log

- 2026-09-26: Created.
