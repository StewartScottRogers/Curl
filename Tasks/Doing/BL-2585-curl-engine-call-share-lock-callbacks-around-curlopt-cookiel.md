---
id: BL-2585
title: Curl.Engine: call share lock callbacks around CURLOPT_COOKIELIST and handle cleanup as libcurl does (test506)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Libcurl, Libcurl.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2585 — Curl.Engine: call share lock callbacks around CURLOPT_COOKIELIST and handle cleanup as libcurl does (test506)

## Goal

A share handle's lock and unlock callbacks are called where libcurl 8.22.0 calls them for test506: around `CURLOPT_SHARE`, each `CURLOPT_COOKIELIST` and `curl_easy_cleanup`, and `curl_share_cleanup` answers `CURLSHE_IN_USE` while an easy handle still names the share.

## Context

Found by BL-2527 running test506 through the ported `lib506`: `<verify><stdout>` differs at line 9, expected `lock:   share  [Pigs in space]: 0` after `CURLOPT_SHARE`, got the next announcement. Today the engine calls the callbacks only around a perform (ADR-0606). Compare test506's whole `<stdout>` with upstream's lib/share.c, lib/setopt.c and lib/easy.c.

## Acceptance criteria

- [ ] test506 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the lines still differing are filed as their own task.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 4e518969 904b57ec 3d836d5e.
