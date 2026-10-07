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
completed: 2026-09-27
---
# BL-318 — Record the ADR for how TransferRetrier reads Retry-After dates

## Goal

An ADR records which `Retry-After` HTTP-date forms `RetryAfterHeader` reads and why, marked "Decided by Claude under Stewart's delegation".

## Context

- Decided in BL-208 (2026-09-26) and recorded only in its Notes, because BL-303 held `Documentation/Planning/Decisions` at the time.
- Decision: libcurl 8.21.0 reads a `Retry-After` date with its lenient `Curl_getdate_capped`; `RetryAfterHeader` reads the three HTTP-date forms RFC 9110 names (RFC 1123 `Sun, 27 Sep 2026 05:26:19 GMT`, RFC 850 `Sunday, 27-Sep-26 05:26:19 GMT`, asctime `Sun Sep 27 05:26:19 2026`) with `DateTimeOffset.TryParseExact`, and treats any other text as delay-seconds. Why: BCL only, every conforming server sends one of the three, and the RFC 1123 form was measured to match curl 8.21.0; hand-porting `parsedate.c` for non-conforming dates is not worth the code.

## Acceptance criteria

- [x] An ADR in `Documentation/Planning/Decisions` states the decision and the reason above, marked "Decided by Claude under Stewart's delegation", and `RetryAfterHeader`'s XML remarks name it.

## Notes

- Delivered: ADR-0080 (`Documentation/Planning/Decisions/ADR-0080-retry-after-reads-the-three-rfc-9110-http-date-forms.md`), indexed in the Decisions README; `RetryAfterHeader`'s XML remarks name it. Done directly rather than through `align-and-document`: one ADR and one remark line.
- Found while writing it: ADR-0074 (after BL-208) put a port of `parsedate`, `CurlDateParser`, in `Curl.Protocol.Abstractions`, which `Curl.Core` already references, so the "not worth the code" reason no longer holds. The ADR records the decision as taken and says so under Consequences; filed BL-393 to move `RetryAfterHeader` onto `CurlDateParser` and supersede ADR-0080.
- Verify: `dotnet build` 0 warnings, 0 errors; fast tests green in all 15 test assemblies (Curl.Core.UnitTests 826 passed, 3 skipped).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ADR-0080 records why RetryAfterHeader reads only the three RFC 9110 HTTP-date forms, and its XML remarks name it
