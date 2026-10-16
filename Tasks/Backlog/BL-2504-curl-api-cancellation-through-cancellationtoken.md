---
id: BL-2504
title: Curl.Api: cancellation through CancellationToken
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2501, BL-2500]
touches: [Curl.Api/Cancellation, Curl.Api.UnitTests/Cancellation]
requirement: none
created: 2026-10-16
completed:
---
# BL-2504 — Curl.Api: cancellation through CancellationToken

## Goal

Cancelling the `CancellationToken` passed to any `CurlClient` call aborts the running transfer promptly and throws `OperationCanceledException` (not `CurlException`), whether the transfer is connecting, uploading, downloading or paused.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Abort through the progress callback abort result and unpause a paused transfer so it can see the abort. Distinguish a timeout (`CurlCode.OperationTimedOut`, a `CurlException`) from the caller cancellation.

## Acceptance criteria

- [ ] Tests cancel before the start, during connect, during an upload, during a download and while paused, and show the handle is freed each time; a timeout is a `CurlException`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
