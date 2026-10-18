---
id: BL-2646
title: Resume a POST paused by its read callback when the write callback unpauses it so upstream case 1533 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2539, BL-2728, BL-2729]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2646 — Resume a POST paused by its read callback when the write callback unpauses it so upstream case 1533 passes

## Goal

A 3-byte POST whose read callback answers `CURL_READFUNC_PAUSE` until the response's first bytes arrive, and whose write callback then calls `curl_easy_pause(CURLPAUSE_CONT)`, finishes, and with `CURLOPT_KEEP_SENDING_ON_ERROR` the connection is reused, so upstream case 1533 passes (exit 41, `TEST_ERR_SUCCESS`).

## Context

Found by BL-2539 (port `Lib1533` with its `PausedPoster` in `Curl.Conformance.LibTest.UnitLibrary`). Through the case runner on Windows test1533 ends with `curl did not finish within 20 seconds` and no stderr was kept. The server answers each POST early, before the body (see `Curl.Conformance.UnitTests/UpstreamTestData/test1533.curltest`): the engine must keep receiving while the upload is paused, deliver the response to the write callback, and resume sending when the callback unpauses it from inside the transfer. Expected stderr lines are `<description>: expected: N connections; actual: N connections` for the four requests (1, 1, 1, 0 new connections). The engine is `Curl.Engine.UnitLibrary`.

## Acceptance criteria

- [x] A unit test in `Curl.Engine.UnitTests` reproduces the stall and passes once it is fixed.
- [x] Upstream case 1533 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (it prints no path, OS error text or certificate).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

- 2026-10-18 (lane 2, first pass before any edit): the stall is not in the engine. `CallbackReadStream` and `EasyPauseGate` already wait out a read pause and resume on `curl_easy_pause(CONT)`; the hang is that `Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.WriteAsync` awaits the paused body read before it reads any response, so the write callback that would unpause is never called. `CURLOPT_KEEP_SENDING_ON_ERROR` also reaches no further than `EasyBehaviourOptions`: `HttpRequestOptions` (`Curl.Protocol.Abstractions.UnitLibrary`) has no field for it. Filed BL-2728 (the contract field) and BL-2729 (the handler: receive while the upload is paused, stop or keep sending on an early 401). What is left here once both are Done: map `KeepSendingOnError` in `Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs`, the engine unit test, and case 1533 on the ratchet.
- 2026-10-18 (lane 1): with BL-2728 and BL-2729 Done, `HttpRequestOptionsMapping.Of` now copies `EasyBehaviourOptions.KeepSendingOnError` onto `HttpRequestOptions.KeepSendingOnError`. Tests: `HttpRequestOptionsMappingTests.Of_KeepSendingOnError_IsCopiedFromTheBehaviourOptions` and `ManagedEasyTransferTests.PausedPost.cs` (lib1533's callbacks over `ScriptedConnector` with an early 401: with the flag the body `aaa` is sent, without it the request ends at the head; both finish with `Ok` and deliver the 401 body). `CURL_UPSTREAM_CASE=1533` passes through the manual runner test; the case is pure HTTP over the in-memory sws (no path, OS text or certificate), so it is on the ratchet. Build clean, 42 fast test projects green, `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 2d40087c 2d8c1045 4272a6b9 c4aff572.
- 2026-10-18: Doing -> Backlog. Waits on BL-2728 (KeepSendingOnError in HttpRequestOptions) and BL-2729 (HTTP handler receives while the upload is paused)
- 2026-10-18: Backlog -> Doing. Criteria at claim: 2d40087c 2d8c1045 4272a6b9 c4aff572.
- 2026-10-18: Doing -> Done. A read-callback POST paused until the write callback unpauses it finishes, CURLOPT_KEEP_SENDING_ON_ERROR reaches the HTTP handler, and upstream case 1533 is on the ratchet
