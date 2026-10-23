---
id: BL-3271
title: Land per-handle caches and fix the Engine tests that pin engine-wide sharing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitLibrary/ManagedShareHandle.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/CookieEngine.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.SslSessions.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Preparation.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Cleanup.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Hsts.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.MaxConnects.cs, Curl.Engine.UnitTests/EasyHandleIsolationTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.SslSessions.cs, Curl.Engine.UnitTests/ManagedCurlEngineTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3271 — Land per-handle caches and fix the Engine tests that pin engine-wide sharing

## Goal

Two easy handles from one `CurlLibrary` that no share handle links share no cookies and no connections, a share handle shares exactly the kinds set with `CURLSHOPT_SHARE` and stops after `CURLSHOPT_UNSHARE`, and every Engine test passes with the per-handle caches.

## Context

Split from BL-3204 (report 29: handles of one library leak cookies and connections). The per-handle caches are already written and stashed (see Notes); this task lands them and fixes the Engine tests that still pin the old engine-wide sharing. `ManagedEngineSharedState` has `EngineWide`, `WithOwnCaches()`, `InMulti(multi)` and `MultiCaches`; `CreateEasy` passes `shared.WithOwnCaches()`; `ManagedEasyTransfer.PerformShared` picks share, else engine-wide for `ToolSettings`, else the multi's connections/TLS/DNS with the handle's own cookies/HSTS, else the handle's own; `Duplicate` takes fresh caches. Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/EasyHandleIsolationTests.cs`: two unshared handles, no cookie crosses and B's NUM_CONNECTS is 1.
- [ ] Cookie-only share: cookies cross, connections do not (B's NUM_CONNECTS 1).
- [ ] Connection-only share: the connection is reused, no cookie crosses; DNS-only share: neither cookies nor connections cross.
- [ ] After `CURLSHOPT_UNSHARE` of cookies, a new cookie set by A no longer reaches B (detach both handles first; upstream answers CURLSHE_IN_USE while attached, check `ManagedShareHandle.SetOption`).
- [ ] The seven tests that pinned engine-wide sharing are fixed (Cleanup.cs `DisposeAsync_ToolSettingsSet_LeavesThePooledConnectionForTheNextHandle` with ToolSettings on both handles, `DisposeAsync_HandleInAMultiHandle_LeavesThePooledConnectionForTheNextHandle`, Hsts.cs `DisposeAsync_HandleThatNeverPerformed_HandsTheEnginesHstsCacheToTheWriteCallback`, SslSessions.cs `ImportTlsSession_NoShare_EngineCacheHoldsItForEveryHandleToExport`, the two MaxConnects.cs tests looking at the multi's pool, and `PerformAsync_CookiesUnshared_...`).
- [ ] Build clean with -warnaserror and `dotnet test Curl.Engine.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3204 (over its cost cap).
- Continues the stashed work of BL-3204.

## Log

- 2026-10-23: Created.
