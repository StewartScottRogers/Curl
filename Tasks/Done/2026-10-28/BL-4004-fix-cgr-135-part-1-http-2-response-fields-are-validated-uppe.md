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
completed: 2026-10-28
---
# BL-4004 — Fix CGR-135 part 1: HTTP/2 response fields are validated (upper-case, connection-specific, pseudo-header misuse, trailers)

## Goal

Curl rejects an invalid HTTP/2 response field or trailer as curl 8.18.0 (nghttp2 1.68) does: same exit code, stdout bytes, stderr text and GOAWAY PROTOCOL_ERROR on the wire (CGR-135 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`).

## Context

First half of BL-3664 (split). Its Notes hold REF-LINUX measurements and the plan: `Http2ResponseHead.IndexOfInvalidField(fields, isTrailer)`, `Http2StreamConnection.ReceiveHeadersAsync` / `RejectInvalidFieldAsync`, trailers without END_STREAM shutting the connection down, and the `-Http2Frames` recorder extension. The fix was written and left in the stash; its Curl.Protocol.Http.UnitTests passed (2522).

## Acceptance criteria

- [x] Upper-case names, connection-specific headers (`connection`, `keep-alive`, `proxy-connection`, `transfer-encoding`, `upgrade`, `te` other than `trailers`), unknown/duplicate/misplaced pseudo-headers, `:status 101` and pseudo-headers or upper-case names in trailers fail with exit 16 (exit 8 when a regular field precedes `:status`), message `Invalid HTTP header field was received: frame type: 1, stream: 1, name: [..], value: [..]`, partial head on stdout, GOAWAY PROTOCOL_ERROR.
- [x] Trailer HEADERS without END_STREAM fail with exit 16 `nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`.
- [x] `Record-CurlExchange.ps1 -Http2Frames` exists (merged with BL-3912's -Http2Answer/-Http2NoSettings, DATA `\n` unescape fixed).
- [x] Focused tests in `Cgr135Tests.cs` (ADR-0482) fail before the fix and pass after it; `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3664 (over its cost cap).
- Continues the stashed work of BL-3664.
- The stash also carries the PORT/REF measurements recorded in BL-3664's Notes; commit the code, leave re-measuring to BL-4005.
- Carried over stash 5778da33 onto CGR-136 (21f9cff2e), which had landed its own connection-specific check: `Http2ResponseHead.IndexOfInvalidField(fields, isTrailer)` now holds both (CGR-136's `IsConnectionSpecific` kept as a private helper), and Record-CurlExchange.ps1 keeps both `-Http2HeaderField` and `-Http2Frames`.
- Quality gate: split the validators to stay at complexity 10 or below, parsed the already-validated content-length with `long.Parse` (the null branch was unreachable), and added `unkpseudofirst` (`:foo` before `:status`) to cover the non-`:status` pseudo-header branch: nghttp2 rejects an unknown pseudo-header wherever it stands, so curl writes nothing and exits 16 (follows the measured `unkpseudo` case; not separately measured). Measure-CodeQuality: 0 failing members in Curl.Protocol.Http.UnitLibrary.
- CI on Linux and macOS: the tests drive a scripted in-memory connection, with no path, OS error or certificate, so they are platform-neutral; fast tests green here (Http.UnitTests 2568 passed).

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: d634e8c6 1a07f34e 5d3dc6df 50ac746f.
- 2026-10-28: Doing -> Done. HTTP/2 response fields and trailers are validated as nghttp2 1.68 does (CGR-135 part 1)
