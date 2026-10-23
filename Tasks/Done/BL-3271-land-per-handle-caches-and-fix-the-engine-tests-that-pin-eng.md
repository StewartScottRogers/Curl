---
id: BL-3271
title: Land per-handle caches and fix the Engine tests that pin engine-wide sharing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitLibrary/ManagedShareHandle.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/CookieEngine.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.SslSessions.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Preparation.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Cleanup.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Hsts.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.MaxConnects.cs, Curl.Engine.UnitTests/EasyHandleIsolationTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.SslSessions.cs, Curl.Engine.UnitTests/ManagedCurlEngineTests.cs, Curl.Engine.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3271 — Land per-handle caches and fix the Engine tests that pin engine-wide sharing

## Goal

Two easy handles from one `CurlLibrary` that no share handle links share no cookies and no connections, a share handle shares exactly the kinds set with `CURLSHOPT_SHARE` and stops after `CURLSHOPT_UNSHARE`, and every Engine test passes with the per-handle caches.

## Context

Split from BL-3204 (report 29: handles of one library leak cookies and connections). The per-handle caches are already written and stashed (see Notes); this task lands them and fixes the Engine tests that still pin the old engine-wide sharing. `ManagedEngineSharedState` has `EngineWide`, `WithOwnCaches()`, `InMulti(multi)` and `MultiCaches`; `CreateEasy` passes `shared.WithOwnCaches()`; `ManagedEasyTransfer.PerformShared` picks share, else engine-wide for `ToolSettings`, else the multi's connections/TLS/DNS with the handle's own cookies/HSTS, else the handle's own; `Duplicate` takes fresh caches. Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only.

## Acceptance criteria

- [x] `Curl.Engine.UnitTests/EasyHandleIsolationTests.cs`: two unshared handles, no cookie crosses and B's NUM_CONNECTS is 1.
- [x] Cookie-only share: cookies cross, connections do not (B's NUM_CONNECTS 1).
- [x] Connection-only share: the connection is reused, no cookie crosses; DNS-only share: neither cookies nor connections cross.
- [x] After `CURLSHOPT_UNSHARE` of cookies, a new cookie set by A no longer reaches B (detach both handles first; upstream answers CURLSHE_IN_USE while attached, check `ManagedShareHandle.SetOption`).
- [x] The seven tests that pinned engine-wide sharing are fixed (Cleanup.cs `DisposeAsync_ToolSettingsSet_LeavesThePooledConnectionForTheNextHandle` with ToolSettings on both handles, `DisposeAsync_HandleInAMultiHandle_LeavesThePooledConnectionForTheNextHandle`, Hsts.cs `DisposeAsync_HandleThatNeverPerformed_HandsTheEnginesHstsCacheToTheWriteCallback`, SslSessions.cs `ImportTlsSession_NoShare_EngineCacheHoldsItForEveryHandleToExport`, the two MaxConnects.cs tests looking at the multi's pool, and `PerformAsync_CookiesUnshared_...`).
- [x] Build clean with -warnaserror and `dotnet test Curl.Engine.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3204 (over its cost cap).
- Continues the stashed work of BL-3204.
- Decided (ADR-0789): library easy handles keep the engine's DNS cache, so upstream test1512 (second handle reaches the first's `CURLOPT_RESOLVE` host) stays passing as it does on libcurl 8.22.0; `Duplicate` copies the source's HSTS entries (test1922); the engine closes every handle's and multi's own pool on `DisposeAsync`, which sends the FTP `QUIT` 18 upstream multi cases (525 to 593, 753, 1501, 540's DISCONNECT) expect.
- Values pinned are presence of a cookie and `NUM_CONNECTS` 0 or 1, libcurl's documented per-handle and share behaviour; no C libcurl build is at hand to record, and the upstream ratchet cases (all 20 that first failed) pin the protocol bytes.
- Test fixes: ToolSettings on both handles; the multi cleanup test adds the second handle to the multi; the never-performed HSTS saver writes nothing; the no-share TLS import is exported by the importing handle; MaxConnects tests open the idle connections in the multi's pool; the unshare test detaches both handles before `CURLSHOPT_UNSHARE`.
- Added Curl.Engine.UnitLibrary/CLAUDE.md to touches: its engine-wide sharing paragraph was no longer true; no task in Doing on origin/work/dark-factory names it.
- Verified: Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary ran every test project that reaches the engine (Api, Console, Conformance, Libcurl, Mcp, Engine and the rest) green with 0 failing members. Tests use fakes only and no path or OS text, so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 4692e370 e2e3b72c 3d88e88c 88324f4b d8d1f0e8 ecaac2b5.
- 2026-10-23: Doing -> Done. Per-handle caches landed; seven pinned tests fixed; Engine and every dependent test project green, quality gate clean
