---
id: BL-2585
title: Curl.Engine: call share lock callbacks around CURLOPT_COOKIELIST and handle cleanup as libcurl does (test506)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2585 — Curl.Engine: call share lock callbacks around CURLOPT_COOKIELIST and handle cleanup as libcurl does (test506)

## Goal

A share handle's lock and unlock callbacks are called where libcurl 8.22.0 calls them for test506: around `CURLOPT_SHARE`, each `CURLOPT_COOKIELIST` and `curl_easy_cleanup`, and `curl_share_cleanup` answers `CURLSHE_IN_USE` while an easy handle still names the share.

## Context

Found by BL-2527 running test506 through the ported `lib506`: `<verify><stdout>` differs at line 9, expected `lock:   share  [Pigs in space]: 0` after `CURLOPT_SHARE`, got the next announcement. Today the engine calls the callbacks only around a perform (ADR-0606). Compare test506's whole `<stdout>` with upstream's lib/share.c, lib/setopt.c and lib/easy.c.

## Acceptance criteria

- [x] test506 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the lines still differing are filed as their own task.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-17 (lane 2): the first criterion adds test506 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, so that file was added to `touches`. BL-2528, in Doing, touches the same file, so the task went back to Backlog before any code was written; the board offers it again once BL-2528 leaves Doing. No code was changed.
- 2026-10-17 (lane 1): `ManagedEasyTransfer.Options`' setter now attaches the handle to the share `CURLOPT_SHARE` names at once (`ManagedShareHandle.Reattach`, the `Share` lock pair and the in-use count, as lib/setopt.c), and detaches from the one before; each `CURLOPT_COOKIELIST` item added takes the cookie lock pair while cookies are shared (`LockCookies`); `DisposeAsync` takes the cookie lock for libcurl's cookie flush, then detaches under the `Share` lock. A perform no longer counts or locks `Share`. `curl_share_cleanup` answers `InUse` before calling any lock callback, as test506's "try SHARE_CLEANUP" shows no lock lines. Choice: `curl_easy_reset` (an `Options = new()`) detaches the share too, the simplest reading of a reset handle naming none. test506's `<stdout>` now matches through line 35 (run 1's `PERFORM`); the lock order inside a perform (cookie, DNS, cookie per `Set-Cookie`, DNS at done) is filed as BL-2603. Focused tests: `ManagedShareHandleTests.Options_ShareCookieListAndCleanup_LockAsLibcurlDoesAndCleanupInUseTakesNoLock` and `Options_ShareChangedOrCleanedUp_DetachesFromTheOldAndAttachesOnlyALiveOne`. Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members. The fast run had one failure outside this change, `Curl.Api.UnitTests`' `ResponseBodyStreamTests.DisposeAsync_WhilePaused_AbortsTheTransfer` (drives a fake easy handle, fails alone at this commit), filed as BL-2604; every other project passed (Curl.Engine.UnitTests 1776, Libcurl.UnitTests 1100, Curl.Conformance.UnitTests 3179).

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 4e518969 904b57ec 3d836d5e.
- 2026-10-17: Doing -> Backlog. touches Curl.Conformance.UnitTests/PassingUpstreamCases.txt, which BL-2528 (in Doing) also touches
- 2026-10-17: Backlog -> Doing. Criteria at claim: 4e518969 904b57ec 3d836d5e.
- 2026-10-17: Doing -> Done. CURLOPT_SHARE, CURLOPT_COOKIELIST and curl_easy_cleanup call the share lock callbacks as libcurl does and curl_share_cleanup answers IN_USE without locking; test506's perform lock order filed as BL-2603
