---
id: BL-2648
title: Report connect, pretransfer, starttransfer and total times from the first header callback so upstream case 1541 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2540]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2648 — Report connect, pretransfer, starttransfer and total times from the first header callback so upstream case 1541 passes

## Goal

`curl_easy_getinfo` called from inside the first header callback answers `CURLINFO_CONNECT_TIME_T`, `CURLINFO_PRETRANSFER_TIME_T`, `CURLINFO_STARTTRANSFER_TIME_T` and `CURLINFO_TOTAL_TIME_T` above 0 (with appconnect time and download speed 0), as curl 8.22.0 does (curl issue #13125), so upstream case 1541 passes.

## Context

Found by BL-2540, which ported `lib1541` (`Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs`). Through the runner case 1541's stdout differs at its first line: expected `CURLINFO_CONNECT_TIME_T on 1st header is OK`, got `CURLINFO_CONNECT_TIME_T on 1st header is WRONG: 0`, so the engine fills the transfer's times only once it ends. The info must be live during the transfer; the checks after the transfer are not yet known to pass.

## Acceptance criteria

- [x] A focused unit test in `Curl.Engine.UnitTests` shows the connect, pretransfer, starttransfer and total times read during the header callback are above 0.
- [x] `CURL_UPSTREAM_CASE=1541` run through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 1541 is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `ManagedEasyTransfer` stamps the perform's start (`PerformAsync`) and the first connection taken (`ShowConnectionOnInfo`, the `EngineConnectionIdTransferEvents` callback), and `ShowResponseHeadOnInfo` (BL-2639's hook, run on each head line before the header callback) now also puts connect and pretransfer (start to connection), starttransfer (start to the first head line, kept) and total (start to now) on `Info`. Appconnect and download speed stay 0 until the finished report replaces everything.
- Choice: pretransfer equals connect, as the handle sees no later protocol-setup point; under the curl tool's `ToolEvents` the handle sees no connection, so connect stays 0 there (the runner reads only the finished info).
- Test: `ManagedEasyTransferTests.Info_ReadFromTheFirstHeaderCallback_HoldsTheConnectPretransferStarttransferAndTotalTimesSoFar`, on a clock that moves 1 ms per read. Case 1541 passes through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` and is on the ratchet; lib1541 checks only that times are above 0 or 0, so it does not depend on the platform.
- Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: c5458ae9 536cb59e 3d836d5e.
- 2026-10-18: Doing -> Done. curl_easy_getinfo inside the header callback answers live connect, pretransfer, starttransfer and total times; upstream case 1541 passes
