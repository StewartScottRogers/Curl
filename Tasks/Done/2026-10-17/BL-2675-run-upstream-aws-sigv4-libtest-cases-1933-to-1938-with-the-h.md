---
id: BL-2675
title: Run upstream AWS SigV4 libtest cases 1933 to 1938 with the harness's Debug stand-in and a fixed signing time
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2675 — Run upstream AWS SigV4 libtest cases 1933 to 1938 with the harness's Debug stand-in and a fixed signing time

## Goal

Upstream cases 1933, 1934, 1935, 1936, 1937 and 1938 run (not skipped) through the case runner, and those that pass on Windows hold the ratchet.

## Context

Found by BL-2546, which ported lib1933 to lib1938 as `LibAwsSigV4Request` in `Curl.Conformance.LibTest.UnitLibrary` (registered in `LibTestPrograms.Ported`, unit tested over a fake engine). Each case is skipped: `test1933 is skipped: Curl lacks the feature Debug`. The cases say "this relies on the Debug feature which allows tests to set the time" (`<features>SSL Debug crypto aws</features>`): upstream's debug build fixes the SigV4 signing time with `CURL_FORCETIME`. `UpstreamConformanceTests.cs` (around line 405) already stands in for Debug for the httpsig, HSTS-clock, alt-svc and HTTP/2 cases; extend that stand-in to a case naming the `aws` feature, and make sure the engine the libtest runner hands the program (the case's `Engine`) signs with the fixed time the stand-in uses for the curl tool, finding the project at fault (likely the runner in `Curl.Conformance.UnitLibrary` or the engine's SigV4 clock) and adding it to `touches` before any edit. Record the decision in an ADR if it is a behaviour choice.

## Acceptance criteria

- [x] `CURL_UPSTREAM_CASE=<n>` with `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` runs (does not skip) each of 1933 to 1938; each that passes on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, and each that fails is filed with the board script with its differing lines.
- [x] No added case's output depends on the platform.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The project at fault was `Curl.Engine.UnitLibrary`: `ManagedCurlEngine.CreateDispatcher` passed no `signingClock` to `EngineComposition.CreateProtocolHandlers`, so the libtest engine always signed with the system clock. Added `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` to `touches` (no task in Doing on `origin/work/dark-factory` named them; `git fetch` was refused, so the local ref was read).
- Fix: an internal `signingClock` on `ManagedCurlEngine` (kept on `ManagedEngineSharedState.SigningClock`, copied to share-handle states); `UpstreamConformanceTests.CreateEngine` passes the epoch; `PlatformFor` reports `Debug` to a case naming `aws`; `RunCurlAsync` also signs `--aws-sigv4` commands at the epoch. Decision in ADR-0671.
- Default taken: the libtest engine signs at the epoch for every case (runtests sets `CURL_FORCETIME` for all), not only aws ones; only SigV4 and HTTP signatures read that clock.
- Measured on Windows with `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`: 1934-1938, 1955-1959, 1970, 1971, 1973-1976, 3222, 439, 5026, 5027 pass and are listed (20 cases; all plain HTTP with no file, path or certificate, so platform-neutral). Failing, filed: 1933 (no `Authorization` sent with URL-only credentials; expected line 3 `Authorization: XXX4-HMAC-SHA256 Credential=xxx/19700101/0/127/xxx4_request, ...`, got the blank line) as BL-2693; 1978 (custom headers before `Authorization`, line 3 got `x-amz-meta-test: test2`) as BL-2694; 1972 (random MIME boundary, line 8) as BL-2695. 472 stays skipped for `Unicode`.
- Unit test: `ManagedCurlEngineTests.PerformAsync_SigningClockGivenAndAwsSigV4_SignsAtThatClocksTime`. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members. Fast tests: 42 projects passed, 0 failed.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 5819e099 21e5ddeb c59fcab3 3d836d5e.
- 2026-10-17: Doing -> Done. 20 upstream AWS SigV4 cases (1934-1938 and 15 more) run with an epoch signing clock and hold the ratchet; 1933, 1972, 1978 filed as BL-2693 to BL-2695
