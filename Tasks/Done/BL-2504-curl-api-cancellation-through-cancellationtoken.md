---
id: BL-2504
title: Curl.Api: cancellation through CancellationToken
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2501, BL-2500]
touches: [Curl.Api/Cancellation, Curl.Api.UnitTests/Cancellation, Curl.Api/Messages, Curl.Api/ResponseBodies, Curl.Api/WebSockets, Curl.Api/CLAUDE.md, Curl.Api.UnitTests/ResponseBodies]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2504 — Curl.Api: cancellation through CancellationToken

## Goal

Cancelling the `CancellationToken` passed to any `CurlClient` call aborts the running transfer promptly and throws `OperationCanceledException` (not `CurlException`), whether the transfer is connecting, uploading, downloading or paused.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Abort through the progress callback abort result and unpause a paused transfer so it can see the abort. Distinguish a timeout (`CurlCode.OperationTimedOut`, a `CurlException`) from the caller cancellation.

## Acceptance criteria

- [x] Tests cancel before the start, during connect, during an upload, during a download and while paused, and show the handle is freed each time; a timeout is a `CurlException`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Plan: `TransferCancellation` (`Curl.Api/Cancellation`) sets `CURLOPT_XFERINFOFUNCTION` with `CURLOPT_NOPROGRESS` off; once the token is cancelled the callback answers 1 (abort, `CURLE_ABORTED_BY_CALLBACK`) and the handle is unpaused, after the owner's `onCancel` (the body buffer aborts, so a redelivery is refused rather than paused again). The token still goes to `PerformAsync`. A failure after the caller cancelled throws `OperationCanceledException`; any other, a timeout included, `CurlException`; a token already cancelled creates no handle. Wired into `SendAsync`, `ResponseBodyStream`/`ResponseBodyBuffer` and `ConnectWebSocketAsync`. ADR-0636.
- Touches widened to `Curl.Api/Messages`, `Curl.Api/ResponseBodies`, `Curl.Api/WebSockets`, `Curl.Api/CLAUDE.md` and `Curl.Api.UnitTests/ResponseBodies`: cancellation has to be applied where transfers start and where their failures are thrown. No task in Doing on `origin/work/dark-factory` named any of them (the other Curl.Api tasks touch `Curl.Api/Options/Transport` and `Curl.Api/Mime`).
- Two existing tests changed with the behaviour, not weakened: `ResponseBodyStreamTests.CancellationToken_CancelledWhileTheBodyStreams_AbortsTheTransfer` now expects `OperationCanceledException` carrying the token (this task's goal) and still checks the transfer ended `AbortedByCallback`; `DisposeAsync_WhilePaused_AbortsTheTransfer` now checks the handle was unpaused once and the transfer ended `WriteError` (the aborted buffer refuses the redelivery) instead of `AbortedByCallback` from the perform token.
- Tests: `Curl.Api.UnitTests/Cancellation`. `CancellableEasyTransfer` ignores the perform token and stays still while paused, so only the callback-and-unpause path can stop it. `SendAsync` is cancelled before the start, while connecting, uploading, downloading and paused, and `ConnectWebSocketAsync` before the start and while connecting; each checks the handle was disposed (or never created). Timeouts throw `CurlException` with code 28.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` reported 0 failing members; `dotnet build` clean, 0 warnings; fast tests all passed (Curl.Api.UnitTests 607 passed, 2 skipped as before; Curl.Conformance.UnitTests 3130 passed, `PassingUpstreamCases.txt` unchanged).

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 1c5a95f0 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. Cancelling the token of any CurlClient call aborts the transfer through the progress callback, unpausing a paused one, and throws OperationCanceledException; timeouts stay CurlException
