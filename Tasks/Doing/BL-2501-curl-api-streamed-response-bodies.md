---
id: BL-2501
title: Curl.Api: streamed response bodies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2499]
touches: [Curl.Api/ResponseBodies, Curl.Api.UnitTests/ResponseBodies]
requirement: none
created: 2026-10-16
completed:
---
# BL-2501 — Curl.Api: streamed response bodies

## Goal

`CurlResponse.Body` is a readable `Stream` that yields the body as the engine delivers it, with back-pressure (the write callback pauses the transfer when the reader falls behind) and disposal that aborts the transfer.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Bridge the engine write callback to a bounded buffer with a pipe or channel from the BCL; use `CurlPause` for back-pressure. Disposing the stream before the end aborts and frees the handle.

## Acceptance criteria

- [ ] Tests cover a body read in small chunks, back-pressure pausing and resuming, early disposal aborting the transfer, a transfer failure surfacing as `CurlException` from `ReadAsync`, and cancellation of a pending read.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: fb34720a 4272a6b9 5393a01b 8738c7f6 ae118507.
