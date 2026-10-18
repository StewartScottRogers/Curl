---
id: BL-2625
title: Make upstream case 650 pass: the curl_formadd post with content headers, arrays, files and stdin sends nothing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2625 — Make upstream case 650 pass: the curl_formadd post with content headers, arrays, files and stdin sends nothing

## Goal

Upstream case 650 (lib650, every curl_formadd feature, followed 301 as POST) passes through the runner.

## Context

Found by BL-2534; the ports in `Curl.Conformance.LibTest.UnitLibrary` are unit tested and follow their C sources. Differences: 650 `<verify><protocol> differs at byte 0 (line 1): expected "POST /650 HTTP/1.1\r\n", got the end`, with empty stdout and stderr, so every curl_formadd succeeded and the perform failed before sending (suspects: CURLFORM_FILE "-" from stdin, CURLFORM_FILECONTENT, the multi-file field, or CURLFORM_CONTENTHEADER). Run each with `CURL_UPSTREAM_CASE=<n>` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine or Libcurl project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Cause, found with a scratch engine test of each of lib650's fields: only `CURLFORM_FILE "-"` failed, with
  `CURLE_READ_ERROR` (`Failed to open/read local data from file/application`): a library handle had no standard
  input, so the form build opened a file named `-`. Touches widened (no task in Doing on `origin/work/dark-factory`
  named these): `Curl.Engine.UnitLibrary`/`.UnitTests` (the stdin part), `Curl.Core.UnitLibrary`/`.UnitTests` (the read
  pattern) and `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` (the harness passes the case's `<stdin>`).
- The expected chunk sizes (0x385, 0xab, 0xbb, 0x11b, 0x16, 0x36 with curl 8.22.0's 48-character boundary line)
  show libcurl reads each file part through its read function at most once per buffer fill and passes a file at its
  known size without another read; BL-2624 had done this for read-callback parts only. Core now marks opened file
  parts too (`MultipartBodySegments.AddFileStream`), in fill mode only, so `-F` bodies read as before.
- Decisions (ADR-0683): the stdin part is an unknown-size `EasyMimeCallback` over `Host.StandardInput` with a seek
  callback (`CantSeek` when it cannot), so the 301 re-POST rewinds it; `ManagedCurlEngine`'s internal constructor
  takes the `standardInput` its handles read; a production library handle keeps the empty one rather than taking
  the process's standard input on its own.
- Platform: the case's file and stdin bytes are the harness's own on every platform, and the file name sent is the
  base name of a `%LOGDIR` path; nothing in its output depends on the platform.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Engine.UnitLibrary`: both at 100% line and
  branch; `ConcatenatedReadStream.Fill`/`FillAsync` measured complexity 12, so the loop condition and the segment
  advance moved to `FillsOn` and `PassSegmentWhenDone` (each at most 4); Core tests rerun green after.
- Fast tests: every project passed (Core 1621, Engine 1877, Conformance 3410 passed, 0 failed).

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Done. Upstream case 650 passes: a curl_formadd file named - reads the engine's standard input, sent chunked, and file parts are read once per buffer fill as libcurl does
