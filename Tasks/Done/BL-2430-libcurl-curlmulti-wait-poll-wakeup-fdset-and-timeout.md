---
id: BL-2430
title: Libcurl: CurlMulti wait, poll, wakeup, fdset and timeout
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2429]
touches: [Libcurl/Multi/Wait, Libcurl.UnitTests/Multi/Wait, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2430 — Libcurl: CurlMulti wait, poll, wakeup, fdset and timeout

## Goal

Libcurl `CurlMulti` has `Wait`, `Poll`, `Wakeup`, `FdSet` and `Timeout`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Fake-engine tests pin each result.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Decided in ADR-0582: the waits are async only (`WaitAsync`, `PollAsync`) beside `PerformAsync`,
  since `CurlEasy.Perform` is Libcurl's one sync bridge; a negative `timeout_ms` answers
  `BadFunctionArgument` without the engine, as `multi_wait` does; `FdSet` fills `ISet<CurlSocket>`
  read, write and exception sets from the engine's `GetWaitSockets`, the seam having no fdset.
- Added `Libcurl/CLAUDE.md` to `touches` to keep it true (acceptance criterion); no task in Doing names it.
- The wait area has its own fake (`FakeWaitMultiTransfer`), since `Multi/Core`'s fake is outside `touches`.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members; Libcurl.UnitTests
  640 passed; every fast test project green; `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CurlMulti has WaitAsync, PollAsync, Wakeup, Timeout and FdSet over the engine's IMultiTransfer, pinned by fake-engine tests
