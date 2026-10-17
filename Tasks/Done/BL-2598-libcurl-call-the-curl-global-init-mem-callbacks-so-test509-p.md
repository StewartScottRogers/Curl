---
id: BL-2598
title: Libcurl: call the curl_global_init_mem callbacks so test509 prints Callbacks were invoked!
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2598 — Libcurl: call the curl_global_init_mem callbacks so test509 prints Callbacks were invoked!

## Goal

A `CurlLibrary` made by `CurlGlobal.InitMemory` calls its allocation callbacks for the allocations a transfer handle makes, so upstream test509 prints `Callbacks were invoked!` and passes.

## Context

Found by BL-2589: test509 now runs through `lib509` (`Curl.Conformance.LibTest.UnitLibrary/Lib509.cs`) and fails with `<verify><stdout> differs at byte 0: expected "Callbacks were invoked!\n", got the end`, because `Libcurl/Global/CurlGlobal.cs`'s `InitMemoryCounting` checks the five callbacks and ignores them, so `LibTestCountingAllocator.Seen` stays 0. Upstream libcurl 8.22.0 routes every `malloc`/`strdup`/`calloc` of `curl_easy_init`, `curl_easy_setopt(CURLOPT_USERAGENT)` and `curl_easy_escape` through the callbacks. Decide (ADR) which Libcurl calls invoke which callback - at least `curl_easy_init` (calloc), a string setopt (strdup) and `curl_easy_escape` (malloc), with free on cleanup. Debug with `CURL_UPSTREAM_CASE=509` and the `Manual` test category (see `Curl.Conformance.UnitTests/CLAUDE.md`).

## Acceptance criteria

- [x] A `Libcurl.UnitTests` test pins that `curl_easy_init`, a string option and `curl_easy_escape` on an `InitMemory` library call the given callbacks.
- [x] test509 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision (ADR-0664): an InitMemory library's easy handles allocate through its callbacks - calloc(1, 64) at curl_easy_init and duphandle, strdup per accepted string option, malloc per curl_easy_escape given the handle, free of each at cleanup; realloc never. Pinned in Libcurl.UnitTests/Global/CurlGlobalInitMemoryTests.cs.
- Touches gained Curl.Conformance.LibTest.UnitTests: Lib509Tests pinned the old "callbacks ignored" behaviour (its quiet run is now a plain-init library). No other Doing task named it.
- Curl.Console.UnitTests fails a different 5-6 tests on each run, none about allocations: filed BL-2600. That flakiness also aborted Measure-CodeQuality.ps1 -Library Libcurl before it measured; the new code was checked by hand instead (every method at complexity 1-3, every branch hit by the new tests; EasyAllocations.Calloc folded into ForNewHandle so no branch is unreachable).

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b1181a16 e4dd0d40 3d836d5e.
- 2026-10-17: Doing -> Done. InitMemory libraries call calloc/strdup/malloc/free for easy handles; test509 prints Callbacks were invoked! and is on the ratchet
