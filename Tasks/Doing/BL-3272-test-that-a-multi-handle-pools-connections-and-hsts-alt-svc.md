---
id: BL-3272
title: Test that a multi handle pools connections and HSTS, alt-svc and TLS session caches stay per handle
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3271]
touches: [Curl.Engine.UnitTests/EasyHandleIsolationTests.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.MaxConnects.cs, Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3272 — Test that a multi handle pools connections and HSTS, alt-svc and TLS session caches stay per handle

## Goal

A multi handle still pools connections across the handles added to it, as upstream does, and HSTS, alt-svc and TLS session caches stay per handle unless shared.

## Context

Split from BL-3204. Depends on the per-handle caches of BL-3271. `CreateMulti` in `ManagedCurlEngine.cs` gives each multi its own connection, DNS and TLS-session caches; a handle performing in a multi takes those but keeps its own cookies and HSTS, as upstream. Check alt-svc (`AltSvcTransferCache`) and PSL the same way and fix any leak, each per upstream's share kinds. Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only.

## Acceptance criteria

- [ ] A test in `Curl.Engine.UnitTests/EasyHandleIsolationTests.cs`: two handles added to one multi handle reuse a connection (second NUM_CONNECTS 0) and send no cookie of each other.
- [ ] A test shows HSTS entries learned by one unshared handle are not used by another, and are shared after `CURL_LOCK_DATA_HSTS` is shared.
- [ ] A test shows alt-svc entries and TLS sessions are per handle unless shared (`CURL_LOCK_DATA_SSL_SESSION` for TLS sessions).
- [ ] Any leak found for PSL or alt-svc is fixed with a focused test; build clean with -warnaserror and the Engine fast tests pass.

## Notes

- Split from BL-3204 (over its cost cap).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f252a655 b303fca0 32145c1a 1fbe56e5.
