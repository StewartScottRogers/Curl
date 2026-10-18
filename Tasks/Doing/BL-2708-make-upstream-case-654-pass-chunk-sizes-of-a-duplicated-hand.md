---
id: BL-2708
title: Make upstream case 654 pass: chunk sizes of a duplicated handle's MIME post with a file part and an unknown-size callback part
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2708 — Make upstream case 654 pass: chunk sizes of a duplicated handle's MIME post with a file part and an unknown-size callback part

## Goal

Upstream case 654 (lib654: a three-part MIME post copied by `curl_easy_duphandle`, then freed) passes through the runner.

## Context

Found by BL-2626, which made the original handle post `Content-Length: 0` after `curl_mime_free` and the duplicate post its own copy (ADR-0675). The first request now matches; the duplicate's chunked body still differs: `<verify><protocol> differs at byte 257 (line 13): expected "1c1\r\n", got "c2\r\n"`. curl 8.22.0 sends the greeting part, the file part (`This is data from a file`) and the callback part's headers as one 0x1c1-byte chunk, then each byte the read callback gives (`d`, `u`, `m`, `m`, `y`, `\n`) as its own 1-byte chunk, then the closing boundary (0x36); Curl ends its first chunk after 0xc2 bytes, so it appears to flush a chunk at the file part rather than fill the upload buffer until the callback part's data. Likely the same chunk framing as BL-2624 (case 645, an unknown-size callback part): check it first, and if BL-2624's fix makes 654 pass, list 654 there. Run with `CURL_UPSTREAM_CASE=654` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine project at fault (the multipart body stream in `Curl.Engine.UnitLibrary` or `Curl.Core.UnitLibrary`, or the chunked upload writer) and add it to `touches` before any edit.

## Acceptance criteria

- [ ] Case 654 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or, still failing, is filed with the board script with its differing lines.
- [ ] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 908e81ba 4963d26c 21e5ddeb 4272a6b9.
