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
completed: 2026-10-16
---
# BL-2559 — Curl.Engine: send no User-Agent from a Libcurl easy handle without CURLOPT_USERAGENT, as libcurl does

## Goal

A `ManagedCurlEngine` easy handle whose `EasyHttpOptions.UserAgent` is `null` sends an HTTP request with no `User-Agent` line, as libcurl 8.22.0 does without `CURLOPT_USERAGENT`, while the curl tool keeps sending `User-Agent: curl/8.22.0`.

## Context

Found by BL-2558: an easy GET run through the ratchet's libtest engine (`UpstreamConformanceTests.CreateEngine`) sent `User-Agent: curl/8.22.0`, because `Curl.Protocol.Http`'s `HttpRequestHeadFormatter` falls back to `DefaultUserAgent` when `HttpRequestOptions.UserAgent` is `null`. That default is the curl tool's (`tool_operate.c` sets `CURLOPT_USERAGENT` to `curl/<version>`), not libcurl's: upstream's lib cases (test500 and most libNNN cases) expect no `User-Agent` line, so every libtest port (BL-2527 onwards) needs this. Move the default to the tool's side (the tool passes `curl/8.22.0` when `-A` is not given, e.g. in `Curl.Console`'s `EasyOptionsFromCommandLine.HttpOf` or the mapping the tool path uses) or give the engine a way to say "no default"; whichever keeps every existing `Curl.Console` test and the ratchet unchanged. If the cleanest fix needs `Curl.Console`, add it to `touches` under the lane rules.

## Acceptance criteria

- [x] A test in `Curl.Engine.UnitTests` performs an easy GET with no `UserAgent` and the request bytes hold no `User-Agent` line; with `UserAgent = "x"` they hold `User-Agent: x`.
- [x] The curl tool still sends `User-Agent: curl/8.22.0` by default: the existing `Curl.Console.UnitTests` and the ratchet's `PassingUpstreamCases.txt` cases pass unchanged.
- [x] `Measure-CodeQuality.ps1 -Library` for every production library changed reports 0 failing members.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Plan: engine-only. `ManagedEasyTransfer.WithoutDefaultUserAgent` turns a `null` `EasyHttpOptions.UserAgent` into `""` for a perform with no `ToolSettings` (a Libcurl or Curl.Api handle); the HTTP formatter and the proxy tunnel already send no `User-Agent` for an empty one, as libcurl does for `CURLOPT_USERAGENT ""`. The curl tool always sets `ToolSettings` (`CurlCommandRunner`), so it keeps `curl/8.22.0`. No change was needed in `Curl.Protocol.Http` or `Curl.Console` (ADR-0659).
- Tests: `ManagedEasyTransferTests.PerformAsync_HttpGetWithNoUserAgent_SendsNoUserAgentLine`, `_HttpGetWithUserAgent_SendsIt`, `_HttpGetWithNoUserAgentUnderToolSettings_SendsTheCurlToolsUserAgent`. Eleven engine tests that compare a handle perform with the runner's own context now give the runner side the same options the handle performs with (`WithoutDefaultUserAgent`); every byte else is still compared.
- Measured: fast tests all green (Curl.Console.UnitTests 2823 passed, Curl.Conformance.UnitTests 3144 passed, Curl.Engine.UnitTests 1730 passed); Measure-CodeQuality -Library Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 0e2ba537 1754abd7 12a99eee 4272a6b9.
- 2026-10-16: Doing -> Done. A Libcurl/Curl.Api easy handle without CURLOPT_USERAGENT sends no User-Agent line; the curl tool still sends curl/8.22.0
