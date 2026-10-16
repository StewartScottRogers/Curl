---
id: BL-2501
title: Curl.Api: streamed response bodies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2499]
touches: [Curl.Api/ResponseBodies, Curl.Api.UnitTests/ResponseBodies, Curl.Api/Messages, Curl.Api.UnitTests/Messages, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2501 — Curl.Api: streamed response bodies

## Goal

`CurlResponse.Body` is a readable `Stream` that yields the body as the engine delivers it, with back-pressure (the write callback pauses the transfer when the reader falls behind) and disposal that aborts the transfer.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Bridge the engine write callback to a bounded buffer with a pipe or channel from the BCL; use `CurlPause` for back-pressure. Disposing the stream before the end aborts and frees the handle.

## Acceptance criteria

- [x] Tests cover a body read in small chunks, back-pressure pausing and resuming, early disposal aborting the transfer, a transfer failure surfacing as `CurlException` from `ReadAsync`, and cancellation of a pending read.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Plan (ADR-0633): `Curl.Api/ResponseBodies/ResponseBodyBuffer` is the write callback, a 64 KiB bounded buffer that answers `CURL_WRITEFUNC_PAUSE` when full and calls `Pause(CurlPause.Continue)` once read down to half; `ResponseBodyStream` is the async-only, read-only body that owns the easy handle, disposes it at the end of the transfer and aborts the transfer (write error plus a cancelled perform token) when disposed first. `SendAsync` now returns once the body starts or the transfer ends; status code and version come from the status line, falling back to `TransferInfo`.
- Touches widened to `Curl.Api/Messages`, `Curl.Api.UnitTests/Messages` and `Curl.Api/CLAUDE.md`: the body has to be wired into `SendAsync`, `CurlRequestOptions` and `CurlResponse`, and the criteria require `CLAUDE.md` kept true. No other Doing task on origin/work/dark-factory named them (BL-2500 names only `Curl.Api/RequestBodies`).
- Choices: synchronous `Read` throws `NotSupportedException` (no thread blocking); a single delivery larger than the capacity is taken whole into an empty buffer; the internal overload is `SendWithBodyCapacityAsync` so `cref="CurlClient.SendAsync"` stays unambiguous.
- Existing `SendAsync` tests updated, not weakened: the body test now reads the bytes instead of asserting `Stream.Null`, and the token test checks that cancelling the request token cancels the perform's (now linked) token.
- Found a lost-unpause window in the managed engine's `EasyPauseGate`; filed as BL-2518.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` 0 failing members; fast tests all green (Curl.Api.UnitTests 525 passed, 2 skipped as before); `PassingUpstreamCases.txt` untouched and Curl.Conformance.UnitTests green.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: fb34720a 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. CurlResponse.Body streams the body as the engine delivers it, pauses the transfer when the reader falls behind, and aborts it when disposed early
