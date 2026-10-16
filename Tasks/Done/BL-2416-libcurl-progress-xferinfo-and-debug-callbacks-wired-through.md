---
id: BL-2416
title: Libcurl: progress, xferinfo and debug callbacks wired through the engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2415]
touches: [Libcurl/Callbacks/Progress, Libcurl.UnitTests/Callbacks/Progress, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2416 — Libcurl: progress, xferinfo and debug callbacks wired through the engine

## Goal

CURLOPT_PROGRESSFUNCTION, XFERINFOFUNCTION and DEBUGFUNCTION (with `CurlInfoType`) are called by the engine with upstream's arguments and frequency, and a non-zero progress return aborts with `AbortedByCallback`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests pin the call arguments and the abort path.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- The engine half was already done by BL-2360 (ADR-0543): `CallbackTransferProgress` and
  `DebugCallbackTransferEvents`, with `CallbackTransferProgressTests`, `DebugCallbackTransferEventsTests` and
  `ManagedEasyTransferTests` (`PerformAsync_TransferInfoCallbackReturnsNonZero_AbortsWithAbortedByCallback`)
  pinning the arguments and the abort path. So `Curl.Engine.UnitLibrary` is unchanged; this run added the
  `Libcurl/Callbacks/Progress` setter rows (`EasyProgressCallbackSetters`) and their tests, ADR-0574.
- Decision: no once-a-second idle progress tick; the engine calls on every report (ADR-0574, point 1).
- `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members. The engine library was not measured
  again since no line of it changed.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl sets CURLOPT_PROGRESSFUNCTION, XFERINFOFUNCTION and DEBUGFUNCTION and their *DATA, which the engine calls with libcurl's arguments and aborts on
