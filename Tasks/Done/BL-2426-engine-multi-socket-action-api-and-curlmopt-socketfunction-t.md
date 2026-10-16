---
id: BL-2426
title: Engine: multi socket-action API and CURLMOPT_SOCKETFUNCTION/TIMERFUNCTION
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2425]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2426 — Engine: multi socket-action API and CURLMOPT_SOCKETFUNCTION/TIMERFUNCTION

## Goal

The engine multi supports `SocketAction`, `Assign`, `CURL_POLL_*`, `CURL_CSELECT_*`, `CURL_SOCKET_TIMEOUT`, and calls the socket and timer callbacks.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests drive a transfer purely through socket actions and pin the callback sequence.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan: `SocketActionAsync` and `PerformAsync` share one loop in `ManagedMultiTransfer`; each running transfer gets one engine-numbered socket (from `0x40000000`) told to `CURLMOPT_SOCKETFUNCTION` as `CURL_POLL_IN`, then `CURL_POLL_REMOVE` with the `Assign` value; `CURLMOPT_TIMERFUNCTION` gets 0 on `Add` and at most 10 ms while transfers run, since the managed connections are no descriptors an application could poll. A callback returning -1 answers `AbortedByCallback` and fails every unfinished transfer with `CURLE_ABORTED_BY_CALLBACK` from the next action. Decision recorded in ADR-0598 (decided by Claude under Stewart's delegation).
- Default taken: the socket and events given to `SocketActionAsync` are not read, because every managed transfer runs on its own; what `Libcurl` does with the engine-numbered sockets is BL-2431's.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` 100% line, 100% branch, 0 failing of 678 members, worst CRAP 10. Fast tests: 41 projects pass, `Curl.Engine.UnitTests` 1110 passed. `PassingUpstreamCases.txt` untouched.
- The plan (`Documentation/Planning/Curl-Api-And-libcurl.md`) says sockets are opaque handles over `IConnection`; in the engine they are one number per running transfer, not per connection (ADR-0598). The plan is outside `touches`, so it is left as is.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. The engine multi handle drives transfers through socket actions and calls the socket and timer callbacks
