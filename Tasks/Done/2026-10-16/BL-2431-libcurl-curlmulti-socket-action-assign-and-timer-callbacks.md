---
id: BL-2431
title: Libcurl: CurlMulti socket action, assign and timer callbacks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2429]
touches: [Libcurl/Multi/Sockets, Libcurl.UnitTests/Multi/Sockets]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2431 — Libcurl: CurlMulti socket action, assign and timer callbacks

## Goal

Libcurl `CurlMulti` has `SocketAction`, `Assign` and the `SOCKETFUNCTION`/`TIMERFUNCTION` options with their delegates.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Fake-engine tests pin the callback delegation and the running-handles output.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan (ADR-0588): `Libcurl/Multi/Sockets/CurlMulti.Sockets.cs` adds `SocketActionAsync` and `Assign`,
  straight calls to the seam's `IMultiTransfer`, and internal `SetSocketFunction`/`SetTimerFunction`
  that wrap Libcurl's own `CurlMultiSocketCallback` (given the added `CurlEasy`) and
  `CurlMultiTimerCallback` (given the `CurlMulti`) into `MultiOptions.Socket`/`Timer`.
- Default taken: the setters are internal because BL-2432 (in Doing, `Libcurl/Multi/Settings`) owns the
  one public `SetOption(CurlMultiOption, ...)` for every `CURLMOPT_*`; its `SOCKETFUNCTION` and
  `TIMERFUNCTION` rows should call these rather than wrap the delegates again.
- The tests use their own `FakeSocketMultiTransfer` in `Libcurl.UnitTests/Multi/Sockets`, since
  `Multi/Core`'s fake is outside `touches`.
- `Libcurl/CLAUDE.md` gained one paragraph sentence for `Multi/Sockets` (acceptance criterion 5).
- `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members. `Libcurl.UnitTests`: 635 passed.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl CurlMulti has SocketActionAsync, Assign and the socket/timer callback delegates with internal setters for SetOption
