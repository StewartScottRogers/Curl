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
completed:
---
# BL-3273 — Pin share-handle isolation through the Libcurl API and clear the quality gates for per-handle caches

## Goal

Share-handle isolation is pinned through the Libcurl API, and the whole solution is clean for the per-handle caches: fast tests green, quality gates met, decision recorded.

## Context

Split from BL-3204. Depends on BL-3271 and BL-3272. Libcurl-level tests in `Libcurl.UnitTests/Share/CurlShareIsolationTests.cs` repeat the unshared, cookie-only, connection-only, DNS-only and unshare cases through `curl_share_setopt` and `curl_easy_*`. The ADR goes in `Documentation/Planning/Decisions` (see ADR-0542, which this refines).

## Acceptance criteria

- [ ] `Libcurl.UnitTests/Share/CurlShareIsolationTests.cs`: unshared handles share no cookie or connection; a cookie-only share crosses cookies only; a connection-only share reuses the connection only; DNS-only shares neither; after `CURLSHOPT_UNSHARE` of cookies a new cookie no longer crosses.
- [ ] An ADR records per-handle caches, marked "Decided by Claude under Stewart's delegation".
- [ ] Build clean with -warnaserror and the full fast tests pass.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` shows 0 failing members.

## Notes

- Split from BL-3204 (over its cost cap).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 23e02afb b0167963 2f085cce 4102ab87.
