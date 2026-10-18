---
id: BL-2607
title: Support CURLOPT_IOCTLFUNCTION in Libcurl so upstream cases 547, 551 and 552 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2607 — Support CURLOPT_IOCTLFUNCTION in Libcurl so upstream cases 547, 551 and 552 pass

## Goal

Upstream cases 547, 551 and 552 pass through the runner and are on the ratchet, their rewinds driven by CURLOPT_IOCTLFUNCTION.

## Context

Found by BL-2530. Libcurl has no setter for `CURLOPT_IOCTLFUNCTION` or `CURLOPT_IOCTLDATA`, so `curl_easy_setopt` answers `CURLE_NOT_BUILT_IN` (4) and lib547 (cases 547, 551) and lib552 (552) stop before any request: the runner reports `<verify><protocol> differs at byte 0: expected "POST http://test.remote.example.com/path/547 HTTP/1.1", got the end`. Add an `IoctlCallback` delegate (`CURLIOCMD_RESTARTREAD` in, `CURLIOE_*` out) in `Curl.Api.UnitLibrary/Callbacks`, its setter rows in Libcurl, and have the engine call it to rewind the upload before resending after a 401/407 when no seek callback is set (curl 8.22.0 `lib/transfer.c` `Curl_xfer_rewind`/`cr_in_rewind`). Then change `Lib547.BlurbReader.Ioctl` and `Lib552.DataBuffer.Ioctl`, handed over today as `Func<int,int>`, to the new delegate (`LibTestIoctl` holds the constants).

## Acceptance criteria

- [x] `CURLOPT_IOCTLFUNCTION` and `CURLOPT_IOCTLDATA` are set through Libcurl and the engine calls the callback with `CURLIOCMD_RESTARTREAD` when it must rewind a read-callback upload.
- [x] Cases 547, 551 and 552 pass through the runner and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482), and the cases' output does not depend on the platform.

## Notes

- Added `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` to `touches`: the rewind is the engine's (`ManagedEasyTransfer.CallbackUploadOf`), and no other task in Doing on `origin/work/dark-factory` named either.
- `IoctlCallback` takes an `IoctlCommand` and returns an `IoctlResult` (`curliocmd`, `curlioerr`), typed like `SeekCallback`/`SeekResult`; `LibTestIoctl` is gone, its constants now the enums. The engine rewinds through `IoctlRewoundCallbackReadStream` only when no seek callback is set, a failure answering `CURLE_SEND_FAIL_REWIND` with `ioctl callback returned error <n>`, as curl 8.22.0's `cr_in_rewind`. Straight from upstream, so no ADR.
- Case 555 (lib555 shares lib547's `BlurbReader`) passes too and went on the ratchet with 547, 551 and 552. All four are HTTP through an in-memory proxy with hand-built NTLM or Digest and `!SSPI`, like the listed cases 67 to 69, so their output does not depend on the platform.
- `Measure-CodeQuality.ps1` found `EasyOptionDispatch.SetOption` at complexity 12 (already there before this task); its value check moved to `IsRefused`, and all four libraries now pass every gate.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 40388131 6dc0ad3e 4272a6b9 7d0e6df3.
- 2026-10-17: Doing -> Done. CURLOPT_IOCTLFUNCTION rewinds read-callback uploads; upstream cases 547, 551, 552 and 555 pass and are on the ratchet
