---
id: BL-2410
title: Libcurl: CurlEasy.Pause and Upkeep
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408]
touches: [Libcurl/Easy, Libcurl.UnitTests/Easy, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2410 — Libcurl: CurlEasy.Pause and Upkeep

## Goal

Libcurl `CurlEasy` has `Pause(CurlPause)` (`CURLPAUSE_*`) and `Upkeep()` / `UpkeepAsync` (`curl_easy_upkeep`) over the seam.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Tests pin every `CURLPAUSE_*` combination's result with the fake engine.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- The seam already had `IEasyTransfer.Pause` and `UpkeepAsync` (and `ManagedEasyTransfer` implements both), so this task is the `CurlEasy` side only: `Pause`, `Upkeep` and `UpkeepAsync`, each a call to the live transfer.
- Decision: `Pause` masks the bitmask to `CurlPause.All` before the engine sees it, because libcurl's `curl_easy_pause` reads only the `CURLPAUSE_RECV` and `CURLPAUSE_SEND` bits and ignores the rest. Mirrors upstream, so no ADR.
- `Upkeep` blocks on `UpkeepAsync`, like `Perform`, since `curl_easy_upkeep` is a blocking call; `Libcurl/CLAUDE.md` now names both bridges.
- Added `Libcurl/CLAUDE.md` to `touches` (acceptance criterion 5 keeps it true); no task in Doing on origin/work/dark-factory names it.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` 0 failing members; Libcurl.UnitTests 616 pass (16 new cases); all fast tests green.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CurlEasy.Pause (curl_easy_pause, CURLPAUSE_* masked to RECV|SEND) and Upkeep/UpkeepAsync (curl_easy_upkeep) over the engine seam
