---
id: BL-3273
title: Pin share-handle isolation through the Libcurl API and clear the quality gates for per-handle caches
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3271, BL-3272]
touches: [Libcurl.UnitTests/Share/CurlShareIsolationTests.cs, Curl.Engine.UnitLibrary/ManagedShareHandle.cs, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3273 — Pin share-handle isolation through the Libcurl API and clear the quality gates for per-handle caches

## Goal

Share-handle isolation is pinned through the Libcurl API, and the whole solution is clean for the per-handle caches: fast tests green, quality gates met, decision recorded.

## Context

Split from BL-3204. Depends on BL-3271 and BL-3272. Libcurl-level tests in `Libcurl.UnitTests/Share/CurlShareIsolationTests.cs` repeat the unshared, cookie-only, connection-only, DNS-only and unshare cases through `curl_share_setopt` and `curl_easy_*`. The ADR goes in `Documentation/Planning/Decisions` (see ADR-0542, which this refines).

## Acceptance criteria

- [x] `Libcurl.UnitTests/Share/CurlShareIsolationTests.cs`: unshared handles share no cookie or connection; a cookie-only share crosses cookies only; a connection-only share reuses the connection only; DNS-only shares neither; after `CURLSHOPT_UNSHARE` of cookies a new cookie no longer crosses.
- [x] An ADR records per-handle caches, marked "Decided by Claude under Stewart's delegation".
- [x] Build clean with -warnaserror and the full fast tests pass.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` shows 0 failing members.

## Notes

- Split from BL-3204 (over its cost cap).
- Pipeline `feature` run directly: one test file mirroring `Curl.Engine.UnitTests/EasyHandleIsolationTests` through `curl_share_setopt`/`curl_easy_*`; no production change was needed. The fake engine must put a `PoolingConnector` over the state's `Connections` (as the engine test does), else the scripted connector bypasses the pool and no connection is reused.
- ADR criterion: ADR-0789 (BL-3271) already records per-handle caches, marked "Decided by Claude under Stewart's delegation" and amending ADR-0542; no new ADR, so `Documentation/Planning/Decisions` is unchanged.
- Build -warnaserror clean; fast tests all green (Libcurl.UnitTests 1413 passed). `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members. Tests are platform-neutral (in-memory connector, no paths).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 23e02afb b0167963 2f085cce 4102ab87.
- 2026-10-23: Doing -> Done. Libcurl share-handle isolation pinned through curl_share_setopt and curl_easy_*; Engine quality gates at 0 failing members
