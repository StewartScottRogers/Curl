---
id: BL-2626
title: Make upstream cases 653 and 654 pass: a MIME post follows changes made after CURLOPT_MIMEPOST and curl_mime_free
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2626 — Make upstream cases 653 and 654 pass: a MIME post follows changes made after CURLOPT_MIMEPOST and curl_mime_free

## Goal

Upstream cases 653 (part data changed between two performs) and 654 (curl_mime_free after curl_easy_duphandle unbinds the original handle) pass through the runner.

## Context

Found by BL-2534; the ports in `Curl.Conformance.LibTest.UnitLibrary` are unit tested and follow their C sources. Differences: 653 `<verify><protocol> differs at byte 350 (line 15): expected "Content-Length: 179\r\n", got "Content-Length: 162\r\n"` (the second post resends the first data); 654 `differs at byte 55 (line 4): expected "Content-Length: 0\r\n", got "Transfer-Encoding: chunked\r\n"` (the freed structure is still posted by the original handle). Libcurl takes a snapshot of the MIME structure at setopt time. Run each with `CURL_UPSTREAM_CASE=<n>` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine or Libcurl project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
