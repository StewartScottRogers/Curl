---
id: BL-2648
title: Report connect, pretransfer, starttransfer and total times from the first header callback so upstream case 1541 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2540]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2648 — Report connect, pretransfer, starttransfer and total times from the first header callback so upstream case 1541 passes

## Goal

`curl_easy_getinfo` called from inside the first header callback answers `CURLINFO_CONNECT_TIME_T`, `CURLINFO_PRETRANSFER_TIME_T`, `CURLINFO_STARTTRANSFER_TIME_T` and `CURLINFO_TOTAL_TIME_T` above 0 (with appconnect time and download speed 0), as curl 8.22.0 does (curl issue #13125), so upstream case 1541 passes.

## Context

Found by BL-2540, which ported `lib1541` (`Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs`). Through the runner case 1541's stdout differs at its first line: expected `CURLINFO_CONNECT_TIME_T on 1st header is OK`, got `CURLINFO_CONNECT_TIME_T on 1st header is WRONG: 0`, so the engine fills the transfer's times only once it ends. The info must be live during the transfer; the checks after the transfer are not yet known to pass.

## Acceptance criteria

- [ ] A focused unit test in `Curl.Engine.UnitTests` shows the connect, pretransfer, starttransfer and total times read during the header callback are above 0.
- [ ] `CURL_UPSTREAM_CASE=1541` run through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 1541 is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: c5458ae9 536cb59e 3d836d5e.
