---
id: BL-2497
title: Curl.Api: CurlClient.Create builds the managed engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2496]
touches: [Curl.Api/Curl.Api.csproj, Curl.Api/Composition, Curl.Api/CLAUDE.md, Curl.Api.UnitTests/Composition]
requirement: none
created: 2026-10-16
completed:
---
# BL-2497 — Curl.Api: CurlClient.Create builds the managed engine

## Goal

`CurlClient.Create()` returns a client over the managed engine, so a caller needs no `ICurlEngine` of their own.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. This is the one task that gives `Curl.Api` a reference to `Curl.Engine.UnitLibrary` (and only that); see how `Libcurl` does it for `CurlGlobal.Init()` (BL-2457) and how `Curl.Api/CLAUDE.md` states the rule, and update the rule. The engine is created through its public composition entry point, not by reflection.

## Acceptance criteria

- [ ] `Curl.Api.csproj` references `Curl.Engine.UnitLibrary` and nothing else new; `Create()` is tested with the real engine in a unit test that sends nothing over the network (version info and handle creation only), and the client disposes the engine it built.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
