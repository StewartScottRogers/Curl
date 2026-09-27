---
id: BL-318
title: Record the ADR for how TransferRetrier reads Retry-After dates
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-208]
touches: [Documentation/Planning/Decisions, Curl.Core.UnitLibrary]
requirement: none
created: 2026-09-26
completed:
---
# BL-318 — Record the ADR for how TransferRetrier reads Retry-After dates

## Goal

An ADR records which `Retry-After` HTTP-date forms `RetryAfterHeader` reads and why, marked "Decided by Claude under Stewart's delegation".

## Context

- Decided in BL-208 (2026-09-26) and recorded only in its Notes, because BL-303 held `Documentation/Planning/Decisions` at the time.
- Decision: libcurl 8.21.0 reads a `Retry-After` date with its lenient `Curl_getdate_capped`; `RetryAfterHeader` reads the three HTTP-date forms RFC 9110 names (RFC 1123 `Sun, 27 Sep 2026 05:26:19 GMT`, RFC 850 `Sunday, 27-Sep-26 05:26:19 GMT`, asctime `Sun Sep 27 05:26:19 2026`) with `DateTimeOffset.TryParseExact`, and treats any other text as delay-seconds. Why: BCL only, every conforming server sends one of the three, and the RFC 1123 form was measured to match curl 8.21.0; hand-porting `parsedate.c` for non-conforming dates is not worth the code.

## Acceptance criteria

- [ ] An ADR in `Documentation/Planning/Decisions` states the decision and the reason above, marked "Decided by Claude under Stewart's delegation", and `RetryAfterHeader`'s XML remarks name it.

## Notes

## Log

- 2026-09-26: Created.
