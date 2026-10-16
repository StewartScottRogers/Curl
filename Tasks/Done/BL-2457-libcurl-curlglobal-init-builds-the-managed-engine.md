---
id: BL-2457
title: Libcurl: CurlGlobal.Init() builds the managed engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456]
touches: [Libcurl/Global, Libcurl.UnitTests/Global, Libcurl/Libcurl.csproj, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2457 — Libcurl: CurlGlobal.Init() builds the managed engine

## Goal

Parameterless `CurlGlobal.Init()` creates a library over the managed engine (`Curl.Engine.UnitLibrary`), as `curl_global_init` needs no argument in C.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] `Libcurl.csproj` references `Curl.Engine.UnitLibrary` (a project reference, no package) and `Libcurl/CLAUDE.md` says so, replacing its "empty of logic" text.
- [x] A test shows `CurlGlobal.Init()` returns a library whose `CreateEasy()` is backed by `ManagedCurlEngine`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Added `CurlGlobal.Init(CurlGlobalFlags flags = Default)`, which builds `new ManagedCurlEngine()` (system clock, real network, empty easy error text until BL-2450), and an internal `CurlLibrary.Engine` so the test can show the engine type without a transfer. Decision recorded in ADR-0577.
- Added `Libcurl/CLAUDE.md` to `touches`: the first acceptance criterion edits it, and no task in Doing on `origin/work/dark-factory` names it. `Curl.Engine.UnitLibrary`'s CLAUDE.md already says it sits behind `Libcurl` and is BL-2416's area, so it is left alone.
- Overloading `Init` made the bare `cref="Init"` ambiguous (CS0419); the crefs now name `Init(CurlGlobalFlags)`.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` reports Libcurl at 100% line, 100% branch, 0 failing members. Fast tests: 0 failed; Libcurl.UnitTests 438 passed.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CurlGlobal.Init() builds a library over ManagedCurlEngine; Libcurl references Curl.Engine.UnitLibrary
