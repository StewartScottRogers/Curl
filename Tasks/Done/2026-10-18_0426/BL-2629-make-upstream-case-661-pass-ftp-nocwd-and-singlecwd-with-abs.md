---
id: BL-2629
title: Make upstream case 661 pass: FTP NOCWD and SINGLECWD with absolute and home-relative paths
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2629 — Make upstream case 661 pass: FTP NOCWD and SINGLECWD with absolute and home-relative paths

## Goal

Upstream case 661 (lib661, CURLOPT_FTP_FILEMETHOD SINGLECWD and NOCWD across fresh, reused and reset handles) passes through the runner.

## Context

Found by BL-2534; the ports in `Curl.Conformance.LibTest.UnitLibrary` are unit tested and follow their C sources. Differences: 661 `<verify><protocol> differs at byte 111 (line 11): expected "QUIT\r\n", got "EPSV\r\n"`: after the first RETR-less failure of the SINGLECWD download of `//folderA/661` the engine goes on to EPSV where curl quits. Run each with `CURL_UPSTREAM_CASE=<n>` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine or Libcurl project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Two causes, both fixed (ADR-0684). (1) The engine pools every handle's connections engine-wide, so after `curl_easy_cleanup` the next fresh handle reused the FTP connection with no `QUIT` and no login; libcurl's handle owns its pool. Now `ConnectionCache.OpeningOwner` (AsyncLocal) tags each connection a library handle's perform opens (`PoolEntry.Owner`) and `ManagedEasyTransfer.DisposeAsync` closes that handle's idle ones (`CloseIdleOwnedByAsync`), FTP sending `QUIT`. Not for a handle in a multi handle or the curl tool's (`ToolSettings`). (2) An FTP `-I` transfer remembered no directory, so the reused connection sent `CWD /` before the next NOCWD transfer of home; `FtpSession.ReportHeadAsync` now remembers it, as curl's `ftp_done`.
- Touches widened to Curl.Engine, Curl.Networking and Curl.Protocol.Ftp (and their tests): no other task was in Doing on origin/work/dark-factory.
- First try keyed connections by `ConnectionOpenedEvent.ConnectionNumber`; that is the TCP connector's number, not the pool entry's, so it missed the second handle's connection. The owner tag avoids numbering.
- Focused tests: `ConnectionCacheTests.CloseIdleOwnedByAsync_*`, `OpeningOwner_SetInsideAnAsyncMethod_IsUndoneWhenItReturns`, `ManagedEasyTransferTests.Cleanup.cs` (fresh handle after cleanup opens anew; tool and multi handles keep it), `FtpProtocolHandlerKeptConnectionTests.ExecuteAsync_SecondNoBodyUrlOfTheHomeDirectoryAfterANoBodyOne_SendsNoCwd`.
- Case 661 is FTP command bytes only: no path separators, drive letters, OS error text or certificates, so it is platform-neutral.
- Measure-CodeQuality for the three libraries: 0 failing members. Fast tests: 42 projects passed, 0 failed.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Done. Upstream case 661 passes and is on the ratchet: curl_easy_cleanup closes the handle's own connections (FTP QUIT) and an FTP -I transfer remembers its directory
