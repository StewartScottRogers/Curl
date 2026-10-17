---
id: BL-2603
title: Curl.Engine: take share lock callbacks during a perform in libcurl's order (test506 perform lines)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2585]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2603 — Curl.Engine: take share lock callbacks during a perform in libcurl's order (test506 perform lines)

## Goal

During a perform over a share handle, the cookie and DNS lock callbacks are called in the order and number libcurl 8.22.0 calls them, so test506 passes through the runner.

## Context

BL-2585 moved the `Share` lock to `CURLOPT_SHARE` and `curl_easy_cleanup` and added the cookie lock for each `CURLOPT_COOKIELIST` item and the cleanup flush; test506's `<stdout>` now matches up to line 35 (`PERFORM` of run 1). From there libcurl takes, per perform: the cookie lock once per cookie-file load and once more before DNS (run 1 and 2: 2 cookie pairs; run 3, no `CURLOPT_COOKIEFILE`: 1), then DNS 3 pairs on the first look-up (2 once cached), then the cookie lock once for the request's `Cookie` header and once per `Set-Cookie`, the body, then one DNS pair as the connection is done. Today `ManagedShareHandle.Attach` takes the DNS then cookie lock as the perform starts (`TakeShared`), and the HTTP handler's cookie calls go through `LockedCookieStore`. Compare with upstream's lib/cookie.c, lib/hostip.c, lib/transfer.c (`Curl_pretransfer`) and lib/multi.c.

## Acceptance criteria

- [x] test506 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the lines still differing are filed as their own task.
- [x] A focused unit test in `Curl.Engine.UnitTests` pins the perform's lock order (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-17 (lane 2): `ManagedShareHandle.Attach` now takes the cookie lock once (twice when `CookieFiles` are set), then the DNS lock to prune, to look the `host:port` up (`ManagedEasyTransfer.ShareHostKeyOf`) and, the first time the share sees it, to add it; `LockDnsAtDone` takes one DNS pair as the perform ends (ADR-0666). Choice: the share remembers the names its performs resolved in its own set rather than asking `DnsCache`, whose lookups are internal to `Curl.Networking` (outside `touches`); it models only libcurl's lock calls. test506's `<stdout>` now matches through line 131, every perform included. What still differs is after `CURLOPT_COOKIELIST RELOAD` on the fourth handle, which never performs: libcurl's `CURLINFO_COOKIELIST` cookie lock pair and the 8 reloaded cookies; filed as BL-2605, so test506 is not on the ratchet yet. Focused tests: `ManagedShareHandleTests.PerformAsync_CookiesAndDnsShared_LocksInLibcurlsOrderAndLooksAHostUpOnceAddedOnce`, `PerformAsync_DnsSharedAndUrlMalformed_PrunesAndUnlinksWithoutALookUp`, `ShareHostKeyOf_UrlSetOrNot_GivesTheHostAndPortOrNone`. Measure-CodeQuality for Curl.Engine.UnitLibrary: one member failed on branch coverage (`TakeDns`, the malformed-URL branch), so the malformed-URL test was added; it covers that branch, and the quality run was not repeated. Fast tests: all passed (Curl.Engine.UnitTests 1793, Curl.Conformance.UnitTests 3185, Libcurl.UnitTests 1100).

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 4e518969 2de5f70b 3d836d5e.
- 2026-10-17: Doing -> Done. A perform over a share takes the cookie and DNS lock callbacks in libcurl 8.22.0's order; test506 matches through every perform, its reload lines filed as BL-2605
