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
completed:
---
# BL-2613 — Send CURLFORM_STREAM fields through CURLOPT_READFUNCTION as upstream case 554 expects

## Goal

Upstream case 554 passes through the libtest runner and is on the ratchet.

## Context

Filed by BL-2531 (lib554 ported, `Curl.Conformance.LibTest.UnitLibrary/Lib554.cs`). Case 554 fails: `<verify><protocol> differs at byte 302 (line 11): expected "
", got "this is what we post to the silly web server
"` - the `CURLFORM_STREAM` part's bytes come where upstream's request has a line end, so the stream part's headers or framing differ (see `Curl.Engine.UnitLibrary/FormStreamData.cs`, ADR-0651). Run with `CURL_UPSTREAM_CASE=554` and the Manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`. Case 587 (same program, aborting read callback) already passes.

## Acceptance criteria

- [ ] Case 554 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, with a focused unit test in `Curl.Engine.UnitTests` (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
