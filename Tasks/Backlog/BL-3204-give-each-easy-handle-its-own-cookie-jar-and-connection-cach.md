---
id: BL-3204
title: Give each easy handle its own cookie jar and connection cache unless a share handle links them
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitLibrary/ManagedShareHandle.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/CookieEngine.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.SslSessions.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Preparation.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Cleanup.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Hsts.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.MaxConnects.cs, Curl.Engine.UnitTests/EasyHandleIsolationTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.SslSessions.cs, Curl.Engine.UnitTests/ManagedCurlEngineTests.cs, Libcurl.UnitTests/Share/CurlShareIsolationTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3204 — Give each easy handle its own cookie jar and connection cache unless a share handle links them

## Goal

Two easy handles from one `CurlLibrary` that no share handle links share no cookies, connections or DNS cache; a share handle shares exactly the data kinds set with `CURLSHOPT_SHARE` and stops after `CURLSHOPT_UNSHARE`; a multi handle still pools connections, as upstream does.

## Context

Stewart reported on 2026-10-22 (Libcurl API conformance batch, report 29). Security issue. Repro: from one CurlLibrary create two easy handles not attached to any share handle, both with the cookie engine on (CookieFile=""). Handle A gets /setcookie, then handle B requests /echo: B sends `Cookie: b=2; s=1; e=5; d=4; c=3; a=1`; upstream sends none. The connection cache is shared the same way: B reuses A's connection (NUM_CONNECTS 0 for B, 1 upstream). Setting only CURL_LOCK_DATA_DNS or only CURL_LOCK_DATA_COOKIE shares the connection cache too. An application serving several users with separate handles leaks one session's cookies, and any connection-bound authentication (NTLM, Negotiate), into another's. Start at `ManagedEngineSharedState.cs` (what lives per library versus per handle) and `ManagedShareHandle.cs`. Check HSTS, alt-svc, TLS sessions and PSL the same way and fix any that leak (each per upstream's share kinds).

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/EasyHandleIsolationTests.cs`: two unshared handles, no cookie crosses and B's NUM_CONNECTS is 1.
- [ ] Cookie-only share: cookies cross, connections do not (B's NUM_CONNECTS 1).
- [ ] Connection-only share: the connection is reused, no cookie crosses; DNS-only share: neither cookies nor connections cross.
- [ ] After `CURLSHOPT_UNSHARE` of cookies, a new cookie set by A no longer reaches B.
- [ ] Two handles added to one multi handle still reuse a connection, as upstream does; a test shows HSTS, alt-svc and TLS session caches are per handle unless shared.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- 2026-10-22 (lane 7, first pass, no code edited): the leak is by design today. `ManagedCurlEngine.CreateEasy`
  hands every handle the one engine-wide `ManagedEngineSharedState` (ADR-0542), so connections, TLS sessions,
  DNS, cookies and HSTS cross handles; alt-svc is per handle already (`AltSvcTransferCache`, check it).
  Plan: in `CreateEasy` give each library handle its own caches (the second `ManagedEngineSharedState`
  constructor already builds a state over given caches); `CreateMulti` (in `ManagedCurlEngine.cs`, so
  `ManagedMultiTransfer.cs`, held by BL-3206, need not change) gives each multi its own connection, DNS and
  TLS-session caches, passing their close-idle and clear callbacks to the multi, and a handle performing in
  a multi (`shared.MultiOwners`) takes that multi's connections, DNS and TLS sessions but keeps its own
  cookies and HSTS, as upstream. Tool attempts (`ToolSettings` set, Curl.Console's `ToolCurlEngine`) keep
  the engine-wide caches, as the curl tool shares all kinds through its own share handle. `Duplicate`
  (`curl_easy_duphandle`) must give the clone fresh caches, not the parent's, and the per-perform choice of
  state (`PerformShared`, `OpeningOwner`, `CloseIdleOwnedByAsync`) lives in `ManagedEasyTransfer.cs`, so
  that file is needed. Tests that pin the old sharing and change: `ManagedEasyTransferTests.cs:597`
  (`TwoHandlesOfOneEngine_ShareTheCookiesAndThePooledConnection`), `ManagedEasyTransferTests.SslSessions.cs:74`
  (`GoesToTheEngineCache`) and `ManagedCurlEngineTests.cs` (`engine.Shared.Hsts`). Added those files to
  touches. `ManagedEasyTransfer.cs` is held by BL-3199 (Doing), so requeued to Backlog until it is free.
- 2026-10-23 (lane 2, second claim; ran short of the cost cap, code left uncommitted for the stash): implemented the plan.
  `ManagedEngineSharedState` gained `EngineWide`, `WithOwnCaches()`, `InMulti(multi)` and `MultiCaches` (a
  ConditionalWeakTable from multi handle to its caches); `CreateEasy` passes `shared.WithOwnCaches()`; `CreateMulti` gives
  each multi its own caches and registers them; `ManagedEasyTransfer.PerformShared` picks share, else engine-wide for
  `ToolSettings`, else the multi's connections/TLS/DNS with the handle's own cookies/HSTS, else the handle's own.
  `Duplicate` takes fresh caches (Lifecycle.cs), cleanup closes idle connections in the engine pool for tool handles, and
  the HSTS write callback saves the handle's cache (Preparation.cs). Added those two files and three test files to
  touches (no task in Doing on origin/work/dark-factory names them). New `EasyHandleIsolationTests.cs` (8 tests); the two
  old pinning tests in ManagedEasyTransferTests.cs and .SslSessions.cs updated. Build clean with -warnaserror.
  Engine fast tests: 7 fail, all pinning the old engine-wide sharing. Fix them on the next claim:
  Cleanup.cs `DisposeAsync_ToolSettingsSet_LeavesThePooledConnectionForTheNextHandle` (give the second handle
  ToolSettings too) and `DisposeAsync_HandleInAMultiHandle_LeavesThePooledConnectionForTheNextHandle`; Hsts.cs:106
  `DisposeAsync_HandleThatNeverPerformed_HandsTheEnginesHstsCacheToTheWriteCallback` (now the handle's own cache);
  SslSessions.cs `ImportTlsSession_NoShare_EngineCacheHoldsItForEveryHandleToExport`; MaxConnects.cs two tests (look at
  the multi's pool, not `engine.Shared.Connections`); and the new `PerformAsync_CookiesUnshared_...` (an AreEqual fails:
  check whether `Unshare` while handles are attached answers Ok in `ManagedShareHandle.SetOption`, upstream answers
  CURLSHE_IN_USE, so detach both handles first). Then Libcurl.UnitTests, the full fast run,
  `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`, and an ADR (per-handle caches, decided by Claude under
  Stewart's delegation).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 4692e370 e2e3b72c 3d88e88c 1734c136 22e7cb9c 3639a5e6.
- 2026-10-22: Doing -> Backlog. Needs Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, which BL-3199 (Doing) holds; plan recorded under Notes
- 2026-10-23: Backlog -> Doing. Criteria at claim: 4692e370 e2e3b72c 3d88e88c 1734c136 22e7cb9c 3639a5e6.
- 2026-10-23: Doing -> Backlog. Out of this run's cost cap with 7 Engine tests still pinning engine-wide sharing; per-handle caches implemented and stashed, remaining steps under Notes
