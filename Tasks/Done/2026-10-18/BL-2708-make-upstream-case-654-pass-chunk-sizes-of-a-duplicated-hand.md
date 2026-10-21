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
completed: 2026-10-18
---
# BL-2708 — Make upstream case 654 pass: chunk sizes of a duplicated handle's MIME post with a file part and an unknown-size callback part

## Goal

Upstream case 654 (lib654: a three-part MIME post copied by `curl_easy_duphandle`, then freed) passes through the runner.

## Context

Found by BL-2626, which made the original handle post `Content-Length: 0` after `curl_mime_free` and the duplicate post its own copy (ADR-0675). The first request now matches; the duplicate's chunked body still differs: `<verify><protocol> differs at byte 257 (line 13): expected "1c1\r\n", got "c2\r\n"`. curl 8.22.0 sends the greeting part, the file part (`This is data from a file`) and the callback part's headers as one 0x1c1-byte chunk, then each byte the read callback gives (`d`, `u`, `m`, `m`, `y`, `\n`) as its own 1-byte chunk, then the closing boundary (0x36); Curl ends its first chunk after 0xc2 bytes, so it appears to flush a chunk at the file part rather than fill the upload buffer until the callback part's data. Likely the same chunk framing as BL-2624 (case 645, an unknown-size callback part): check it first, and if BL-2624's fix makes 654 pass, list 654 there. Run with `CURL_UPSTREAM_CASE=654` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine project at fault (the multipart body stream in `Curl.Engine.UnitLibrary` or `Curl.Core.UnitLibrary`, or the chunked upload writer) and add it to `touches` before any edit.

## Acceptance criteria

- [x] Case 654 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or, still failing, is filed with the board script with its differing lines.
- [x] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Measured first: BL-2624 (Done) already fixed the cause - its fill-across read of reader segments in Curl.Core's multipart body stream fills one chunk across the greeting part, the file part and the callback part's headers up to the first callback byte. With no code change, `CURL_UPSTREAM_CASE=654` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` pass on Windows, so 654 is added to the ratchet. No product fix here, so no new unit test (BL-2624 added the focused Core test).
- Platform: the case compares protocol bytes only, with the boundary stripped; the file part shows only the base name `file654.txt` and LF-only content the harness writes as given, so the output does not depend on the platform.
- Fast tests: 42 projects, 0 failed; `dotnet build` 0 warnings, 0 errors.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 908e81ba 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Done. Upstream case 654 passes and is on the ratchet: a duplicated handle's chunked MIME post fills its first chunk up to the callback part's data
