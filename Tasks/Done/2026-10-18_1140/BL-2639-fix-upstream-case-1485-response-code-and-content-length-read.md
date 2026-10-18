---
id: BL-2639
title: Fix upstream case 1485: response code and Content-Length readable from the header callback
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2639 — Fix upstream case 1485: response code and Content-Length readable from the header callback

## Goal

Upstream case 1485 passes through the runner and holds the ratchet.

## Context

Found by BL-2537, which ported lib1485 (`Curl.Conformance.LibTest.UnitLibrary/Lib1485.cs`) and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. At the blank line that ends the response headers, the program's header callback calls `curl_easy_getinfo` for `CURLINFO_RESPONSE_CODE` (must be 100 to 999) and, for a 2xx, `CURLINFO_CONTENT_LENGTH_DOWNLOAD_T` (must be known, 7 here), and answers `CURLE_WRITE_ERROR` otherwise. The runner reports: `test1485 fails: the program's stdout against <reply><data> differs at byte 55 (line 5): expected "123456\n", got the end` - the headers reach stdout but the body does not, so one of those infos is not yet set while the header callback runs (in curl both are set once the headers are parsed, before the end-of-headers callback). Look at where Libcurl's `EasyInfoDispatch` reads `TransferInfo.ResponseCode` and `ContentLengthDownload` and where the engine fills them during the transfer; find the project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] test1485 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; if it still fails it is filed with the board script with its differing lines.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The fault was in `Curl.Engine.UnitLibrary`, not Libcurl: `ManagedEasyTransfer` set `Info` only when the transfer finished, so a header callback read `ResponseCode` 0 and `ContentLengthDownload` -1. Added `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` to `touches`; no task in Doing on `origin/work/dark-factory` named them.
- Fix: `TransferHeaderStore` keeps the live `ResponseCode` (the last status line that is not a CONNECT reply's; 0 when the status does not parse) and `ContentLength` (the final head's last `Content-Length`, reset to -1 by each status line, a 1xx head's ignored), and calls `ResponseHeadRead` on each; `PerformAsync` wires it to put both on `Info` before the line reaches the header callback. The finished report still replaces `Info`. Only `http`, `https`, `ws` and `wss` record headers, so other schemes are unchanged.
- Tests: five new `TransferHeaderStoreTests` and `ManagedEasyTransferTests.Info_ReadFromTheHeaderCallbackAtTheEndOfTheHead_HoldsTheResponseCodeAndContentLength`. test1485 is HTTP only, with no paths or OS text, so it does not depend on the platform.
- `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 100% line and branch; `StartHead` reached complexity 14, so the new code moved to `ReadResponseCode`, putting `StartHead` back to the shape it had when it passed.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 5af69292 c59fcab3 3d836d5e.
- 2026-10-18: Doing -> Done. Upstream case 1485 passes and holds the ratchet: a header callback reads the response code and Content-Length while the transfer runs
