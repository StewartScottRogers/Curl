---
id: BL-2429
title: Libcurl: CurlMulti with add, remove, perform, info_read and cleanup
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408]
touches: [Libcurl/Multi/Core, Libcurl.UnitTests/Multi/Core, Libcurl/Easy/CurlEasy.cs, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2429 — Libcurl: CurlMulti with add, remove, perform, info_read and cleanup

## Goal

Libcurl has `CurlMulti` (`curl_multi_init/_add_handle/_remove_handle/_perform/_info_read/_cleanup`) over `IMultiTransfer`, with `CurlMultiCode` results and the `CurlMultiMessage` queue.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Fake-engine tests pin add of a handle already added, remove, the running count and info_read.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Added `Libcurl/Easy/CurlEasy.cs` to touches: `CurlMulti` needs the easy handle's engine transfer, so `CurlEasy` gains an internal `Transfer` property; no task in Doing on `origin/work/dark-factory` touches `Libcurl/Easy`. `Libcurl/CLAUDE.md` added for the acceptance criterion that keeps it true.
- Decisions in ADR-0567: `CurlMulti` is partial in namespace `Libcurl.Multi` (BL-2430 to BL-2432 add parts in sibling folders); `InfoRead` answers Libcurl's own `CurlMessage` naming the added `CurlEasy`; null or cleaned-up handles answer `BadEasyHandle` and a handle this multi holds `AddedAlready` without calling the engine; no synchronous `Perform`.
- `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members. `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CurlMulti add, remove, perform, info_read and cleanup over IMultiTransfer; fake-engine tests; quality gates 0 failing
