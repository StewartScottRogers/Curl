---
id: BL-2637
title: Fix upstream case 757: a callback MIME part reused as a child part is read once per post
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2637 — Fix upstream case 757: a callback MIME part reused as a child part is read once per post

## Goal

Upstream case 757 passes through the runner and holds the ratchet.

## Context

Found by BL-2536, which ported lib757 (`Curl.Conformance.LibTest.UnitLibrary/Lib757.cs`) and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. The program posts a MIME structure with one `curl_mime_data_cb` part (size 24, a read callback that copies all 24 bytes whenever the buffer is at least 24 long, and a seek callback that always succeeds), then reuses that structure as `curl_mime_subparts` of a second structure's part and posts again. The runner reports: `test757 fails: <verify><protocol> differs at byte 288 (line 11): expected "<title>fun-times</title>\r\n", got "<title>fun-times</title><title>fun-times</title><title>fun-times</title>\r\n"`, so the engine keeps calling the read callback after it has read the part's declared size (libcurl stops at the size given to `curl_mime_data_cb`), or does not rewind between posts. Likely in the MIME body reader of the HTTP engine; find the project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] test757 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; if it still fails it is filed with the board script with its differing lines.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: test757 already passed when claimed (`CURL_UPSTREAM_CASE=757`, and the ratchet row said "test757 passes; add 757 to PassingUpstreamCases.txt"). The fix is BL-2613's (6701e8234): `Curl.Engine`'s `MimeCallbackReadStream` stops asking a sized `curl_mime_data_cb` part's callback once it has given the part's size, as curl 8.22.0's `read_part_content` does. Its focused unit tests are already in `Curl.Engine.UnitTests` (`MimeCallbackReadStreamTests.Read_KnownSizeAllGiven_EndsWithoutAskingTheCallbackAgain` and its `ReadAsync` twin), so this run changes no production code and adds no new test; it only lists 757 on the ratchet.
- Platform check: the case is an in-memory HTTP MIME post with no path, file or certificate, so its output does not depend on the platform.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 6cae2d89 c59fcab3 3d836d5e.
- 2026-10-18: Doing -> Done. test757 passes through the runner (BL-2613's sized MIME callback fix) and holds the ratchet
