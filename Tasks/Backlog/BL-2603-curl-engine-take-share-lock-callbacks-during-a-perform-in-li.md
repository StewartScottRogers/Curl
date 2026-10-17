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
completed:
---
# BL-2603 — Curl.Engine: take share lock callbacks during a perform in libcurl's order (test506 perform lines)

## Goal

During a perform over a share handle, the cookie and DNS lock callbacks are called in the order and number libcurl 8.22.0 calls them, so test506 passes through the runner.

## Context

BL-2585 moved the `Share` lock to `CURLOPT_SHARE` and `curl_easy_cleanup` and added the cookie lock for each `CURLOPT_COOKIELIST` item and the cleanup flush; test506's `<stdout>` now matches up to line 35 (`PERFORM` of run 1). From there libcurl takes, per perform: the cookie lock once per cookie-file load and once more before DNS (run 1 and 2: 2 cookie pairs; run 3, no `CURLOPT_COOKIEFILE`: 1), then DNS 3 pairs on the first look-up (2 once cached), then the cookie lock once for the request's `Cookie` header and once per `Set-Cookie`, the body, then one DNS pair as the connection is done. Today `ManagedShareHandle.Attach` takes the DNS then cookie lock as the perform starts (`TakeShared`), and the HTTP handler's cookie calls go through `LockedCookieStore`. Compare with upstream's lib/cookie.c, lib/hostip.c, lib/transfer.c (`Curl_pretransfer`) and lib/multi.c.

## Acceptance criteria

- [ ] test506 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the lines still differing are filed as their own task.
- [ ] A focused unit test in `Curl.Engine.UnitTests` pins the perform's lock order (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
