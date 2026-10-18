---
id: BL-2613
title: Send CURLFORM_STREAM fields through CURLOPT_READFUNCTION as upstream case 554 expects
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2613 — Send CURLFORM_STREAM fields through CURLOPT_READFUNCTION as upstream case 554 expects

## Goal

Upstream case 554 passes through the libtest runner and is on the ratchet.

## Context

Filed by BL-2531 (lib554 ported, `Curl.Conformance.LibTest.UnitLibrary/Lib554.cs`). Case 554 fails: `<verify><protocol> differs at byte 302 (line 11): expected "
", got "this is what we post to the silly web server
"` - the `CURLFORM_STREAM` part's bytes come where upstream's request has a line end, so the stream part's headers or framing differ (see `Curl.Engine.UnitLibrary/FormStreamData.cs`, ADR-0651). Run with `CURL_UPSTREAM_CASE=554` and the Manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`. Case 587 (same program, aborting read callback) already passes.

## Acceptance criteria

- [x] Case 554 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, with a focused unit test in `Curl.Engine.UnitTests` (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

Cause: `MimeCallbackReadStream` kept asking a sized `curl_mime_data_cb` / `CURLFORM_STREAM` part's read callback after it had given its size, so lib554's reader (one byte a call across every stream, as it gets no stream pointer) poured the second stream into the first part. curl 8.22.0's `read_part_content` spares the read once `offset >= datasize`; the stream now does the same (no buffer clamp, as upstream). Focused tests in `MimeCallbackReadStreamTests` (Read, ReadAsync, unknown size). Case 554 passes; added to the ratchet - an HTTP form post with no path, OS error or certificate, so platform-neutral. Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members. Small one-class fix, so the feature pipeline's subagent stages were done in-session to stay inside the cost cap.


## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: a826d0ff 4272a6b9.
- 2026-10-18: Doing -> Done. Upstream case 554 passes: a sized form-stream or mime callback part stops reading at its size
