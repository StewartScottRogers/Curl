---
id: BL-2607
title: Support CURLOPT_IOCTLFUNCTION in Libcurl so upstream cases 547, 551 and 552 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2607 — Support CURLOPT_IOCTLFUNCTION in Libcurl so upstream cases 547, 551 and 552 pass

## Goal

Upstream cases 547, 551 and 552 pass through the runner and are on the ratchet, their rewinds driven by CURLOPT_IOCTLFUNCTION.

## Context

Found by BL-2530. Libcurl has no setter for `CURLOPT_IOCTLFUNCTION` or `CURLOPT_IOCTLDATA`, so `curl_easy_setopt` answers `CURLE_NOT_BUILT_IN` (4) and lib547 (cases 547, 551) and lib552 (552) stop before any request: the runner reports `<verify><protocol> differs at byte 0: expected "POST http://test.remote.example.com/path/547 HTTP/1.1", got the end`. Add an `IoctlCallback` delegate (`CURLIOCMD_RESTARTREAD` in, `CURLIOE_*` out) in `Curl.Api.UnitLibrary/Callbacks`, its setter rows in Libcurl, and have the engine call it to rewind the upload before resending after a 401/407 when no seek callback is set (curl 8.22.0 `lib/transfer.c` `Curl_xfer_rewind`/`cr_in_rewind`). Then change `Lib547.BlurbReader.Ioctl` and `Lib552.DataBuffer.Ioctl`, handed over today as `Func<int,int>`, to the new delegate (`LibTestIoctl` holds the constants).

## Acceptance criteria

- [ ] `CURLOPT_IOCTLFUNCTION` and `CURLOPT_IOCTLDATA` are set through Libcurl and the engine calls the callback with `CURLIOCMD_RESTARTREAD` when it must rewind a read-callback upload.
- [ ] Cases 547, 551 and 552 pass through the runner and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482), and the cases' output does not depend on the platform.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 40388131 6dc0ad3e 4272a6b9 7d0e6df3.
