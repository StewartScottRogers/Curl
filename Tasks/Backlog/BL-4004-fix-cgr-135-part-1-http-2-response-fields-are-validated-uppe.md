---
id: BL-4004
title: Fix CGR-135 part 1: HTTP/2 response fields are validated (upper-case, connection-specific, pseudo-header misuse, trailers)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3667, BL-3668, BL-3979]
touches: [Curl.Protocol.Http.UnitLibrary/Http2ResponseHead.cs, Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Record-CurlExchange.ps1, Curl.Protocol.Http.UnitTests/Cgr135Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4004 — Fix CGR-135 part 1: HTTP/2 response fields are validated (upper-case, connection-specific, pseudo-header misuse, trailers)

## Goal

Curl rejects an invalid HTTP/2 response field or trailer as curl 8.18.0 (nghttp2 1.68) does: same exit code, stdout bytes, stderr text and GOAWAY PROTOCOL_ERROR on the wire (CGR-135 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`).

## Context

First half of BL-3664 (split). Its Notes hold REF-LINUX measurements and the plan: `Http2ResponseHead.IndexOfInvalidField(fields, isTrailer)`, `Http2StreamConnection.ReceiveHeadersAsync` / `RejectInvalidFieldAsync`, trailers without END_STREAM shutting the connection down, and the `-Http2Frames` recorder extension. The fix was written and left in the stash; its Curl.Protocol.Http.UnitTests passed (2522).

## Acceptance criteria

- [ ] Upper-case names, connection-specific headers (`connection`, `keep-alive`, `proxy-connection`, `transfer-encoding`, `upgrade`, `te` other than `trailers`), unknown/duplicate/misplaced pseudo-headers, `:status 101` and pseudo-headers or upper-case names in trailers fail with exit 16 (exit 8 when a regular field precedes `:status`), message `Invalid HTTP header field was received: frame type: 1, stream: 1, name: [..], value: [..]`, partial head on stdout, GOAWAY PROTOCOL_ERROR.
- [ ] Trailer HEADERS without END_STREAM fail with exit 16 `nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`.
- [ ] `Record-CurlExchange.ps1 -Http2Frames` exists (merged with BL-3912's -Http2Answer/-Http2NoSettings, DATA `\n` unescape fixed).
- [ ] Focused tests in `Cgr135Tests.cs` (ADR-0482) fail before the fix and pass after it; `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3664 (over its cost cap).
- Continues the stashed work of BL-3664.
- The stash also carries the PORT/REF measurements recorded in BL-3664's Notes; commit the code, leave re-measuring to BL-4005.

## Log

- 2026-10-28: Created.
