---
id: BL-2468
title: Engine: CURLMOPT_MAXCONNECTS caps the shared connection cache's idle connections
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2428]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2468 — Engine: CURLMOPT_MAXCONNECTS caps the shared connection cache's idle connections

## Goal

A multi handle's `MultiOptions.MaxConnects` (`CURLMOPT_MAXCONNECTS`) caps the idle connections the engine's shared `ConnectionCache` keeps: once a transfer of the multi ends and more idle connections than the cap remain, the oldest idle ones are closed, as libcurl's connection cache does; 0 keeps libcurl's default of 4 times the transfers added.

## Context

BL-2428 (ADR-0605) applied `PIPELINING`, `MAX_HOST_CONNECTIONS`, `MAX_TOTAL_CONNECTIONS` and `MAX_CONCURRENT_STREAMS` in `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs`, but `MaxConnects` needs the cache to close idle entries beyond a count, and `Curl.Networking.UnitLibrary/ConnectionCache.cs` has only `CloseIdleAsync` (all of them). Add a trim to the cache (e.g. `CloseIdleBeyondAsync(int keep)`, oldest first) and hand it to `ManagedMultiTransfer` as `ManagedCurlEngine.CreateMulti` hands it `CloseIdleAsync`. Check curl 8.22.0's `lib/conncache.c` for when the cap is applied and what it counts.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` pins the trim closing the oldest idle connections first and leaving leased ones open.
- [x] `Curl.Engine.UnitTests` pins a multi with `MaxConnects = 1` leaving one idle connection after two transfers end, and the default (0) keeping 4 times the transfers added.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary,Curl.Engine.UnitLibrary` reports 0 failing members.
- [x] ADR-0605 and `Curl.Engine.UnitLibrary/CLAUDE.md` say `MaxConnects` is applied.

## Notes

- `ConnectionCache.CloseIdleBeyondAsync(long keep)` closes the oldest idle connections until no more than
  `keep` connections, idle and in use (`Idle` + `Leased`), remain; never a leased one. Counting the ones in use
  too follows libcurl's pool, which compares its whole connection count with `CURLMOPT_MAXCONNECTS` and closes
  its oldest idle connection when a transfer hands one back. libcurl closes one per hand-back; closing down to
  the cap at once reaches the same count. Decided by Claude under Stewart's delegation; recorded as ADR-0605's
  BL-2468 amendment (decision 5).
- `ManagedMultiTransfer` calls it (new constructor delegate `closeIdleConnectionsBeyond`) each time it queues a
  finished transfer's `CURLMSG_DONE`, with `MaxConnects`, or 4 for each transfer in the handle (libcurl's
  `num_easy * 4`) when it is 0 or less; `DisposeAsync` awaits the closes.
- Tests: `ConnectionCacheTests.CloseIdleBeyondAsync_*` (order, leased kept, keep 0 / negative) and
  `ManagedMultiTransferTests.MaxConnects.cs` (cap 1 after two transfers, default 4 per transfer with 1 and 2
  transfers, no trim while a transfer runs). Measure-CodeQuality for both libraries: 0 failing members.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. CURLMOPT_MAXCONNECTS now closes the engine's oldest idle connections beyond the cap (default 4 per transfer) when a multi transfer ends
