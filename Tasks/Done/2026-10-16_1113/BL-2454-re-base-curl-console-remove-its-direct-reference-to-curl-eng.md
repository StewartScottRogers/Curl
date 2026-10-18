---
id: BL-2454
title: Re-base Curl.Console: remove its direct reference to Curl.Engine.UnitLibrary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2453, BL-2415, BL-2435, BL-2437, BL-2440, BL-2444, BL-2423]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2454 — Re-base Curl.Console: remove its direct reference to Curl.Engine.UnitLibrary

## Goal

Curl.Console references `Libcurl` and not `Curl.Engine.UnitLibrary` directly (it still reaches the engine through `CurlGlobal.Init()`), and its CLAUDE.md says so.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] `Curl.Console.csproj` has no `Curl.Engine.UnitLibrary` reference and builds; `InProcessCurl.RunAsync`'s public signature is unchanged.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Console` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- 2026-10-16: `Curl.Console.csproj` drops its `Curl.Engine.UnitLibrary` reference; the engine flows in transitively through `Libcurl`'s public reference, and the 15 tool files that name engine types (`EngineComposition`, `TransferPerformer`, `CurlErrorBuffer`) build unchanged. Decision and why in ADR-0629. No .cs change, so no new public member; `Curl.Console/CLAUDE.md` now says what it references. Build clean; fast tests green (Console 2795, Conformance 3130, Libcurl 1084, Engine 1654 passed, 0 failed); ratchet file untouched; `Measure-CodeQuality.ps1 -Library Curl.Console` reports 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Curl.Console references Libcurl and reaches the engine only through it
