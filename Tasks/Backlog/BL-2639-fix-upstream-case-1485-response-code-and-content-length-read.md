---
id: BL-2639
title: Fix upstream case 1485: response code and Content-Length readable from the header callback
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2639 — Fix upstream case 1485: response code and Content-Length readable from the header callback

## Goal

Upstream case 1485 passes through the runner and holds the ratchet.

## Context

Found by BL-2537, which ported lib1485 (`Curl.Conformance.LibTest.UnitLibrary/Lib1485.cs`) and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. At the blank line that ends the response headers, the program's header callback calls `curl_easy_getinfo` for `CURLINFO_RESPONSE_CODE` (must be 100 to 999) and, for a 2xx, `CURLINFO_CONTENT_LENGTH_DOWNLOAD_T` (must be known, 7 here), and answers `CURLE_WRITE_ERROR` otherwise. The runner reports: `test1485 fails: the program's stdout against <reply><data> differs at byte 55 (line 5): expected "123456\n", got the end` - the headers reach stdout but the body does not, so one of those infos is not yet set while the header callback runs (in curl both are set once the headers are parsed, before the end-of-headers callback). Look at where Libcurl's `EasyInfoDispatch` reads `TransferInfo.ResponseCode` and `ContentLengthDownload` and where the engine fills them during the transfer; find the project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [ ] test1485 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; if it still fails it is filed with the board script with its differing lines.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
