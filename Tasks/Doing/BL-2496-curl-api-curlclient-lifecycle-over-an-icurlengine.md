---
id: BL-2496
title: Curl.Api: CurlClient lifecycle over an ICurlEngine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Client, Curl.Api.UnitTests/Client]
requirement: none
created: 2026-10-16
completed:
---
# BL-2496 — Curl.Api: CurlClient lifecycle over an ICurlEngine

## Goal

`new CurlClient(ICurlEngine)` is an `IAsyncDisposable` that owns the engine (or not, by a constructor flag), creates easy handles on demand and refuses use after `DisposeAsync` with `ObjectDisposedException`.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Read `ICurlEngine` and `IEasyTransfer` as they are now. `CurlClientOptions` holds client-wide defaults (user agent, timeouts) that later tasks extend.

## Acceptance criteria

- [ ] Tests with a fake `ICurlEngine` cover construction, handle creation, owning and not owning the engine, double dispose and use after dispose.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
