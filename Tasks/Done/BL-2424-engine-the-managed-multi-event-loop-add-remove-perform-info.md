---
id: BL-2424
title: Engine: the managed multi event loop (add, remove, perform, info_read)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2424 — Engine: the managed multi event loop (add, remove, perform, info_read)

## Goal

`ManagedCurlEngine.CreateMulti()` returns an `IMultiTransfer` that runs added easy transfers concurrently over the engine's connections, with add/remove/perform/info_read (`CURLMSG_DONE` carrying each handle's `CurlCode`).

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests run several fake transfers to completion and show the message queue and remove-while-running.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan: the internal `ManagedMultiTransfer` is the engine's `IMultiTransfer`. Each added transfer runs as its own `IEasyTransfer.PerformAsync`, started by the next multi `PerformAsync`, which never waits and queues one `CURLMSG_DONE` per finished perform in add order. Decisions in ADR-0586 (Decided by Claude under Stewart's delegation).
- The seam matched the plan apart from names (`IMultiTransfer.TryReadInfo`, `CurlMultiMessage`), so the plan needed no correction.
- A transfer is in one of the engine's multi handles at a time (`ManagedEngineSharedState.MultiOwners`, by reference). `Remove` of a transfer in no multi handle answers `Ok`; one in another multi handle answers `BadEasyHandle`, as libcurl's `multi_remove_handle` does.
- Wait, poll, wakeup and timeout (BL-2425), socket action and assign (BL-2426), and get_handles, waitfds, get_offt and notify (BL-2427) throw `NotSupportedException` naming their task, because those tasks own them.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reported 0 failing members. The fast tests all pass (Curl.Engine.UnitTests: 1075 passed).

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-16: Doing -> Done. ManagedCurlEngine.CreateMulti gives a ManagedMultiTransfer: add, remove, perform and info_read run transfers at once with CURLMSG_DONE results
