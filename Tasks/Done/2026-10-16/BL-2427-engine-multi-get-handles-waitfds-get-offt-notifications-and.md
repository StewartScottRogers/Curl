---
id: BL-2427
title: Engine: multi get_handles, waitfds, get_offt, notifications and network change
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2426]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2427 — Engine: multi get_handles, waitfds, get_offt, notifications and network change

## Goal

The engine multi has `GetHandles`, `WaitFds`, `GetInfo` (`CURLMINFO_*`), `NotifyEnable/Disable` and `NetworkChanged`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests pin each answer.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- 2026-10-16 (lane 2): `ManagedMultiTransfer` now answers `GetHandles` (add order), `GetWaitSockets`
  (one `PollIn` socket per started, unfinished transfer, numbered as the socket callback is told;
  too-short array is `OutOfMemory` with the full count), `GetInfo` (`CURLMINFO_*` as curl 8.22.0,
  pending 0 until BL-2428's limits), `EnableNotification`/`DisableNotification` (`InfoRead` on the
  first queued message, `EasyDone` per finished transfer, sent at the end of each perform or socket
  action) and `CURLMOPT_NETWORK_CHANGED` as an action on the `Options` setter. Decisions in ADR-0604.
- Touches widened to `Curl.Networking.UnitLibrary` and `Curl.Networking.UnitTests`: clearing for a
  network change needs `ConnectionCache.CloseIdleAsync` and `DnsCache.Clear`, which did not exist and
  are internal to that library otherwise. No task in Doing on `origin/work/dark-factory` named them.
- The multi takes the two clears as delegates (`Func<ValueTask>`, `Action`) so its tests record them;
  the cache methods are tested in `Curl.Networking.UnitTests` (`ConnectionCacheTests`, `DnsCacheTests`).
- No upstream source or real curl was reachable from the lane: the count, ordering and
  unknown-notification answers follow upstream's code as recalled and are marked unmeasured in ADR-0604.
- The plan's phase 3 rows match the seam as it is; nothing to correct there.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. The engine multi answers get_handles, waitfds, get_offt and notify enable/disable, and CURLMOPT_NETWORK_CHANGED closes idle connections and empties the DNS cache (ADR-0604)
