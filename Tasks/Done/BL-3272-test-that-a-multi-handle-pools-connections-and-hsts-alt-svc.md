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
completed: 2026-10-23
---
# BL-3272 — Test that a multi handle pools connections and HSTS, alt-svc and TLS session caches stay per handle

## Goal

A multi handle still pools connections across the handles added to it, as upstream does, and HSTS, alt-svc and TLS session caches stay per handle unless shared.

## Context

Split from BL-3204. Depends on the per-handle caches of BL-3271. `CreateMulti` in `ManagedCurlEngine.cs` gives each multi its own connection, DNS and TLS-session caches; a handle performing in a multi takes those but keeps its own cookies and HSTS, as upstream. Check alt-svc (`AltSvcTransferCache`) and PSL the same way and fix any leak, each per upstream's share kinds. Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only.

## Acceptance criteria

- [x] A test in `Curl.Engine.UnitTests/EasyHandleIsolationTests.cs`: two handles added to one multi handle reuse a connection (second NUM_CONNECTS 0) and send no cookie of each other.
- [x] A test shows HSTS entries learned by one unshared handle are not used by another, and are shared after `CURL_LOCK_DATA_HSTS` is shared.
- [x] A test shows alt-svc entries and TLS sessions are per handle unless shared (`CURL_LOCK_DATA_SSL_SESSION` for TLS sessions).
- [x] Any leak found for PSL or alt-svc is fixed with a focused test; build clean with -warnaserror and the Engine fast tests pass.

## Notes

- Split from BL-3204 (over its cost cap).
- Tests only: no production code changed, so no Measure-CodeQuality run was needed. `EasyHandleIsolationTests` gained the multi test's no-cookie check (`PerformAsync_TwoHandlesInOneMulti_ReuseTheMultisConnectionAndSendNoCookieOfEachOther`), two HSTS tests (the first handle learns `Strict-Transport-Security` over `https`, the second's `http` URL stays on port 80 unshared and switches to 443 under `CURL_LOCK_DATA_HSTS`), an alt-svc test (an `h1` alternative learned by one handle routes that handle's next perform and not another's) and two TLS session tests (the dispatcher factory records each perform's state: unshared handles get different `TlsSessionCache`s, handles sharing `CURL_LOCK_DATA_SSL_SESSION` the share's one).
- Upstream basis (libcurl 8.22.0 source, no output text pinned): `curl_share_setopt` has no alt-svc kind, so alt-svc stays per easy handle always, which `ManagedEasyTransfer.handleAltSvc` already does; the multi handle owns the TLS session cache, which `InMulti` already takes. PSL is `Curl.Cookies`' one embedded read-only snapshot, so no per-handle state can leak. No leak found, nothing to fix.
- Seen, left alone: `ManagedShareHandle`'s own `HstsTransferPolicy` ignores the engine's `CURL_HSTS_HTTP` stand-in (debug build only, harness only), so the HSTS tests learn over `https` instead.
- Criterion 4: `dotnet test -warnaserror` on Curl.Engine.UnitTests: 2391 passed, 5 skipped, 0 failed; the change is platform-neutral (fakes only, no paths).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f252a655 b303fca0 32145c1a 1fbe56e5.
- 2026-10-23: Doing -> Done. Tests pin multi connection pooling without cookie leaks and per-handle HSTS, alt-svc and TLS session caches unless shared
