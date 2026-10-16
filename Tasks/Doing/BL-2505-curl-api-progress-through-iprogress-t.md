---
id: BL-2505
title: Curl.Api: progress through IProgress<T>
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2504]
touches: [Curl.Api/Progress, Curl.Api.UnitTests/Progress]
requirement: none
created: 2026-10-16
completed:
---
# BL-2505 — Curl.Api: progress through IProgress<T>

## Goal

`CurlRequest` takes an `IProgress<CurlTransferProgress>` reporting download and upload totals, speed and phase from the xferinfo callback.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Use the seam progress callback; report on the thread the engine calls from (the caller `IProgress<T>` decides marshalling). Reports stop after the transfer ends.

## Acceptance criteria

- [ ] Tests with a fake engine that calls the progress callback show each report fields, no report after completion and that a throwing `IProgress<T>` aborts the transfer with `CurlException`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 76c93e4c 4272a6b9 5393a01b 8738c7f6 ae118507.
