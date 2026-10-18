---
id: BL-2624
title: Make upstream case 645 pass: chunk sizes of a MIME post with unknown-size callback parts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2624 — Make upstream case 645 pass: chunk sizes of a MIME post with unknown-size callback parts

## Goal

Upstream case 645 (lib643 with unknown-size callback parts, sent chunked) passes through the runner.

## Context

Found by BL-2534; the ports in `Curl.Conformance.LibTest.UnitLibrary` are unit tested and follow their C sources. Differences: 645 `<verify><protocol> differs at byte 182 (line 8): expected "7c\r\n", got "7b\r\n"` (the first chunk is one byte short; case 643, the same program with sizes known, passes). Run each with `CURL_UPSTREAM_CASE=<n>` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine or Libcurl project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Cause: curl 8.22.0's `read_part_content` calls a `curl_mime_data_cb` read callback at most once per buffer fill (`hasread`, `STOP_FILLING`) but fills across parts otherwise, so the first chunk is the part headers plus the first callback byte (0x7c). Our `Curl.Core` `ConcatenatedReadStream` returned after each segment, so the headers went alone (0x7b). Fix in `Curl.Core.UnitLibrary` (added to `touches`; no task in Doing on `origin/work/dark-factory` named it): `MultipartBodySegments.AddCallbackStream` marks reader segments and the stream fills across segments, reading each marked one once per read.
- Choice: the fill-across mode applies only to bodies holding a reader part, so `-F` bodies (files, text) keep reading one segment per read and no other case's bytes move. Not modelled: curl spares the read (no `hasread`) for a sized part already at its end; that changes only chunk splits of a sized, chunked callback body, which no case shows.
- Measured: case 645 passes through the runner on Windows and is on the ratchet; it compares protocol bytes only (boundary stripped), no path, file or OS text, so it is platform-neutral. Fast tests: 42 projects, 0 failed. Measure-CodeQuality for Curl.Core.UnitLibrary: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Done. Upstream case 645 passes: a chunked MIME post with unknown-size callback parts chunks as curl's buffer fill does
