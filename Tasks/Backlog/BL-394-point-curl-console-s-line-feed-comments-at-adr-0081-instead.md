---
id: BL-394
title: Point Curl.Console's line-feed comments at ADR-0081 instead of ADR-0040
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Console]
requirement: none
created: 2026-09-27
completed:
---
# BL-394 — Point Curl.Console's line-feed comments at ADR-0081 instead of ADR-0040

## Goal

Every comment in `Curl.Console` that cites the `-w` line-feed decision names ADR-0081, its number since BL-326.

## Context

- BL-326 (2026-09-27) renumbered `ADR-0040-w-standard-output-line-feeds-...` to ADR-0081 because ADR-0040 is the HTTP `-m`/`--connect-timeout` decision. `Curl.Console` was outside BL-326's `touches` (BL-132 held it), so two citations still say ADR-0040:
  - `Curl.Console/CLAUDE.md` (the `LineFeedToCrLfStream` paragraph, "as CR LF (ADR-0040)").
  - `Curl.Console/DiskWriteOutFileOpener.cs` XML doc comment ("on Windows (ADR-0040)").
- Comment and documentation change only; no behaviour change.

## Acceptance criteria

- [ ] `git grep -n "ADR-0040" -- Curl.Console` prints nothing, and both citations above read ADR-0081.
- [ ] `dotnet build` is clean.

## Notes

## Log

- 2026-09-27: Created.
