---
id: BL-2509
title: Curl.Api: concurrent transfers over the multi contract
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2501, BL-2504]
touches: [Curl.Api/Concurrency, Curl.Api.UnitTests/Concurrency]
requirement: none
created: 2026-10-16
completed:
---
# BL-2509 — Curl.Api: concurrent transfers over the multi contract

## Goal

`CurlClient.SendAsync` calls made together run as concurrent transfers on one `IMultiTransfer`, with a connection limit option, per-host limits and each call completing or failing on its own.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to `ICurlEngine.CreateMulti()`, `IMultiTransfer` and `MultiOptions`. One driver loop per client; no thread is blocked while waiting.

## Acceptance criteria

- [ ] Tests with a fake `IMultiTransfer` run many calls together, show the limits are applied, one failure does not fail the others, cancelling one leaves the rest running and disposing the client cancels all.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 2fdc6ccd 4272a6b9 5393a01b 8738c7f6 ae118507.
