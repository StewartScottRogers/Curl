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
completed:
---
# BL-2468 — Engine: CURLMOPT_MAXCONNECTS caps the shared connection cache's idle connections

## Goal

A multi handle's `MultiOptions.MaxConnects` (`CURLMOPT_MAXCONNECTS`) caps the idle connections the engine's shared `ConnectionCache` keeps: once a transfer of the multi ends and more idle connections than the cap remain, the oldest idle ones are closed, as libcurl's connection cache does; 0 keeps libcurl's default of 4 times the transfers added.

## Context

BL-2428 (ADR-0605) applied `PIPELINING`, `MAX_HOST_CONNECTIONS`, `MAX_TOTAL_CONNECTIONS` and `MAX_CONCURRENT_STREAMS` in `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs`, but `MaxConnects` needs the cache to close idle entries beyond a count, and `Curl.Networking.UnitLibrary/ConnectionCache.cs` has only `CloseIdleAsync` (all of them). Add a trim to the cache (e.g. `CloseIdleBeyondAsync(int keep)`, oldest first) and hand it to `ManagedMultiTransfer` as `ManagedCurlEngine.CreateMulti` hands it `CloseIdleAsync`. Check curl 8.22.0's `lib/conncache.c` for when the cap is applied and what it counts.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` pins the trim closing the oldest idle connections first and leaving leased ones open.
- [ ] `Curl.Engine.UnitTests` pins a multi with `MaxConnects = 1` leaving one idle connection after two transfers end, and the default (0) keeping 4 times the transfers added.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary,Curl.Engine.UnitLibrary` reports 0 failing members.
- [ ] ADR-0605 and `Curl.Engine.UnitLibrary/CLAUDE.md` say `MaxConnects` is applied.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
