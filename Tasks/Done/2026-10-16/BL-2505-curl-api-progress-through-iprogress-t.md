---
id: BL-2505
title: Curl.Api: progress through IProgress<T>
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2504]
touches: [Curl.Api/Progress, Curl.Api.UnitTests/Progress, Curl.Api/ResponseBodies, Curl.Api/Messages, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2505 — Curl.Api: progress through IProgress<T>

## Goal

`CurlRequest` takes an `IProgress<CurlTransferProgress>` reporting download and upload totals, speed and phase from the xferinfo callback.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Use the seam progress callback; report on the thread the engine calls from (the caller `IProgress<T>` decides marshalling). Reports stop after the transfer ends.

## Acceptance criteria

- [x] Tests with a fake engine that calls the progress callback show each report fields, no report after completion and that a throwing `IProgress<T>` aborts the transfer with `CurlException`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Plan: `CurlRequest.Progress` (`IProgress<CurlTransferProgress>?`) in `Curl.Api/Progress`; `TransferProgressReporter` is the transfer-info callback, reporting then chaining to `TransferCancellation.Progress`; `ResponseBodyStream` builds it and ends it when perform returns. ADR-0643.
- Choices: speeds come from the handle's `CURLINFO_SPEED_*_T` (no clock needed); `CurlTransferPhase` (Connecting, Uploading, Downloading) is read from the byte counts alone; a throwing report answers abort on that and every later call, so the transfer fails as `CurlException` (`AbortedByCallback`) and the exception never reaches the engine.
- Touches widened to `Curl.Api/ResponseBodies`, `Curl.Api/Messages` and `Curl.Api/CLAUDE.md`: the callback must be wired where the transfer is built and ends; no task in Doing on `origin/work/dark-factory` names them (BL-2503 holds `Curl.Api/Options/Protocol` only).
- The first quality run found `SendWithBodyCapacityAsync`'s await-in-catch rethrow line uncovered (a compiler-generated path); set-up failures are now captured with `ExceptionDispatchInfo` and rethrown after the handles are disposed.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` 100% line, 100% branch, 0 failing members, worst CRAP 8; `dotnet build` 0 warnings; fast tests all passed (Curl.Api.UnitTests 670 passed, 2 skipped as before; Curl.Conformance.UnitTests 3130 passed, `PassingUpstreamCases.txt` unchanged).

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 76c93e4c 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. CurlRequest.Progress reports totals, speeds and phase from the transfer-info callback; a throwing report aborts as CurlException
