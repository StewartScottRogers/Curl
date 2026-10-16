---
id: BL-2408
title: Libcurl: CurlEasy with Perform, PerformAsync and Dispose
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456, BL-2399]
touches: [Libcurl/Easy, Libcurl.UnitTests/Easy, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed:
---
# BL-2408 — Libcurl: CurlEasy with Perform, PerformAsync and Dispose

## Goal

Libcurl has `CurlEasy` (created by the library object) with `SetOption`, `Perform()` (synchronous, blocking on `PerformAsync`, the one sanctioned bridge, documented), `PerformAsync(CancellationToken)` returning `CurlCode`, and `Dispose` as `curl_easy_cleanup`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test with a fake `IEasyTransfer` shows Perform returns the engine's `CurlCode`, passes the handle's `EasyOptions`, and Dispose disposes the transfer.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Decisions in ADR-0561: `CurlLibrary.EasyInit()` is `curl_easy_init`; the handle keeps no options of its own and writes the dispatched copy back to `IEasyTransfer.Options` only on `Ok`; `CurlEasy` is `IDisposable` and `IAsyncDisposable`, cleanup disposes the transfer once, and members used afterwards throw `ObjectDisposedException`.
- Added `Libcurl/CLAUDE.md` to `touches` to keep its area map true (an acceptance criterion); no task in Doing on `origin/work/dark-factory` names it.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` reports 100% line and branch coverage and 0 failing members; `PassingUpstreamCases.txt` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
