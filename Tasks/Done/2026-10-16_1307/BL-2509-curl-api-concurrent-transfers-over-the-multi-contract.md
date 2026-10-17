---
id: BL-2509
title: Curl.Api: concurrent transfers over the multi contract
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2501, BL-2504]
touches: [Curl.Api/Concurrency, Curl.Api.UnitTests/Concurrency, Curl.Api/Client/CurlClient.cs, Curl.Api/Messages/CurlClient.SendAsync.cs, Curl.Api/ResponseBodies/ResponseBodyStream.cs, Curl.Api/ResponseBodies/ResponseBodyBuffer.cs, Curl.Api/CLAUDE.md, Curl.Api.UnitTests/Cancellation/CancellableCurlEngine.cs, Curl.Api.UnitTests/Messages/ScriptedCurlEngine.cs, Curl.Api.UnitTests/Mime/MimeCurlEngine.cs, Curl.Api.UnitTests/RequestBodies/UploadingCurlEngine.cs, Curl.Api.UnitTests/Messages/ScriptedEasyTransfer.cs, Curl.Api.UnitTests/RequestBodies/UploadingEasyTransfer.cs]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2509 — Curl.Api: concurrent transfers over the multi contract

## Goal

`CurlClient.SendAsync` calls made together run as concurrent transfers on one `IMultiTransfer`, with a connection limit option, per-host limits and each call completing or failing on its own.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to `ICurlEngine.CreateMulti()`, `IMultiTransfer` and `MultiOptions`. One driver loop per client; no thread is blocked while waiting.

## Acceptance criteria

- [x] Tests with a fake `IMultiTransfer` run many calls together, show the limits are applied, one failure does not fail the others, cancelling one leaves the rest running and disposing the client cancels all.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Design (ADR-0644): `MultiTransferDriver` (`Curl.Api/Concurrency`) runs every `SendAsync` transfer of a
  client on one multi handle, created lazily on the first transfer and disposed with the client. One
  loop (started by the first transfer, ended when none is left) applies queued adds and removes,
  performs, finishes each `CURLMSG_DONE` transfer and waits in `PollAsync`; `Wakeup` ends the wait when
  a transfer arrives or is cancelled. Only the loop calls the handle.
- `CurlClientOptions.MaxConnections` / `MaxConnectionsPerHost` (0 = no limit, negative throws) set
  `CURLMOPT_MAX_TOTAL_CONNECTIONS` / `CURLMOPT_MAX_HOST_CONNECTIONS`.
- A cancelled call's transfer is removed from the handle and ends `CURLE_ABORTED_BY_CALLBACK`, thrown as
  `OperationCanceledException`. Disposing the client cancels a closing token every body is linked to
  (`ResponseBodyBuffer` now takes it as a second token, so the exception carries the caller's token
  when the caller cancelled, the client's when the client was disposed).
- Touches widened (no task in Doing on origin/work/dark-factory named these files): `Client/CurlClient.cs`
  (DisposeAsync stops the transfers), `Messages/CurlClient.SendAsync.cs` (runs on the driver; setup
  failure restructured so no await sits in a catch, whose rethrow lowering left a branch no test can
  reach), `ResponseBodies/ResponseBodyStream.cs` and `ResponseBodyBuffer.cs`, `CLAUDE.md`, and the four
  test engines that send (`CreateMulti` now returns `EasyPerformingMultiTransfer`) plus the scripted and
  uploading fakes (`Pause` answers Ok, since a client dispose can now unpause a finished handle).
- `ConnectWebSocketAsync` still performs directly: a connect-only transfer is not part of the concurrency.
- Measured: Curl.Api 100% line, 100% branch, 272 members, 0 failing, worst CRAP 8. Fast tests all green
  (Curl.Api.UnitTests 703 passed, 2 skipped as before); PassingUpstreamCases.txt unchanged.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 2fdc6ccd 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. CurlClient.SendAsync calls run concurrently on one multi handle with connection and per-host limits; each call fails or cancels on its own and disposing the client cancels all
