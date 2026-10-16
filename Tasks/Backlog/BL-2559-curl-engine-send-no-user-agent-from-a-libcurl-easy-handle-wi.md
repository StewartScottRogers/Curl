---
id: BL-2559
title: Curl.Engine: send no User-Agent from a Libcurl easy handle without CURLOPT_USERAGENT, as libcurl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2559 — Curl.Engine: send no User-Agent from a Libcurl easy handle without CURLOPT_USERAGENT, as libcurl does

## Goal

A `ManagedCurlEngine` easy handle whose `EasyHttpOptions.UserAgent` is `null` sends an HTTP request with no `User-Agent` line, as libcurl 8.22.0 does without `CURLOPT_USERAGENT`, while the curl tool keeps sending `User-Agent: curl/8.22.0`.

## Context

Found by BL-2558: an easy GET run through the ratchet's libtest engine (`UpstreamConformanceTests.CreateEngine`) sent `User-Agent: curl/8.22.0`, because `Curl.Protocol.Http`'s `HttpRequestHeadFormatter` falls back to `DefaultUserAgent` when `HttpRequestOptions.UserAgent` is `null`. That default is the curl tool's (`tool_operate.c` sets `CURLOPT_USERAGENT` to `curl/<version>`), not libcurl's: upstream's lib cases (test500 and most libNNN cases) expect no `User-Agent` line, so every libtest port (BL-2527 onwards) needs this. Move the default to the tool's side (the tool passes `curl/8.22.0` when `-A` is not given, e.g. in `Curl.Console`'s `EasyOptionsFromCommandLine.HttpOf` or the mapping the tool path uses) or give the engine a way to say "no default"; whichever keeps every existing `Curl.Console` test and the ratchet unchanged. If the cleanest fix needs `Curl.Console`, add it to `touches` under the lane rules.

## Acceptance criteria

- [ ] A test in `Curl.Engine.UnitTests` performs an easy GET with no `UserAgent` and the request bytes hold no `User-Agent` line; with `UserAgent = "x"` they hold `User-Agent: x`.
- [ ] The curl tool still sends `User-Agent: curl/8.22.0` by default: the existing `Curl.Console.UnitTests` and the ratchet's `PassingUpstreamCases.txt` cases pass unchanged.
- [ ] `Measure-CodeQuality.ps1 -Library` for every production library changed reports 0 failing members.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-16: Created.
