---
id: BL-2598
title: Libcurl: call the curl_global_init_mem callbacks so test509 prints Callbacks were invoked!
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2598 — Libcurl: call the curl_global_init_mem callbacks so test509 prints Callbacks were invoked!

## Goal

A `CurlLibrary` made by `CurlGlobal.InitMemory` calls its allocation callbacks for the allocations a transfer handle makes, so upstream test509 prints `Callbacks were invoked!` and passes.

## Context

Found by BL-2589: test509 now runs through `lib509` (`Curl.Conformance.LibTest.UnitLibrary/Lib509.cs`) and fails with `<verify><stdout> differs at byte 0: expected "Callbacks were invoked!\n", got the end`, because `Libcurl/Global/CurlGlobal.cs`'s `InitMemoryCounting` checks the five callbacks and ignores them, so `LibTestCountingAllocator.Seen` stays 0. Upstream libcurl 8.22.0 routes every `malloc`/`strdup`/`calloc` of `curl_easy_init`, `curl_easy_setopt(CURLOPT_USERAGENT)` and `curl_easy_escape` through the callbacks. Decide (ADR) which Libcurl calls invoke which callback - at least `curl_easy_init` (calloc), a string setopt (strdup) and `curl_easy_escape` (malloc), with free on cleanup. Debug with `CURL_UPSTREAM_CASE=509` and the `Manual` test category (see `Curl.Conformance.UnitTests/CLAUDE.md`).

## Acceptance criteria

- [ ] A `Libcurl.UnitTests` test pins that `curl_easy_init`, a string option and `curl_easy_escape` on an `InitMemory` library call the given callbacks.
- [ ] test509 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
