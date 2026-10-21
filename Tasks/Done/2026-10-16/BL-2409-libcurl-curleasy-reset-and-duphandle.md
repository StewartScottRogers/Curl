---
id: BL-2409
title: Libcurl: CurlEasy.Reset and DupHandle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408]
touches: [Libcurl/Easy, Libcurl.UnitTests/Easy, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2409 — Libcurl: CurlEasy.Reset and DupHandle

## Goal

Libcurl `CurlEasy` has `Reset()` (`curl_easy_reset`) and `DupHandle()` (`curl_easy_duphandle`) over `IEasyTransfer.Reset/Duplicate`, with upstream's semantics for what survives each.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Tests show Reset clears options and a duplicate carries the options but not the connection state.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- `CurlEasy.Reset()` calls `IEasyTransfer.Reset()` and `CurlEasy.DupHandle()` wraps `IEasyTransfer.Duplicate()` in a new `CurlEasy` with the same dispatch. What survives each is the seam contract that `ManagedEasyTransfer` already implements: a reset restores `new EasyOptions()` and keeps connections and caches; a duplicate carries the options and tool settings but no connection, info or headers. The plan and the code agree, so the plan needs no correction.
- `FakeEasyTransfer` now models that contract (`OpenConnection` stands in for connection state, and `ResetCount` counts resets), so the Libcurl tests check that `CurlEasy` keeps it.
- Decision: `DupHandle` throws `ObjectDisposedException` on a cleaned-up handle, as every other member does, instead of answering null like `curl_easy_duphandle(NULL)`. This follows ADR-0563, so no new ADR.
- Touches: added `Libcurl/CLAUDE.md` to describe Reset and DupHandle. No task in Doing names it.
- Measured: Libcurl fast tests 380 passed. Full fast run green. `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members. `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl CurlEasy.Reset (curl_easy_reset) and DupHandle (curl_easy_duphandle) over IEasyTransfer.Reset/Duplicate
