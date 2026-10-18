---
id: BL-2675
title: Run upstream AWS SigV4 libtest cases 1933 to 1938 with the harness's Debug stand-in and a fixed signing time
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2675 — Run upstream AWS SigV4 libtest cases 1933 to 1938 with the harness's Debug stand-in and a fixed signing time

## Goal

Upstream cases 1933, 1934, 1935, 1936, 1937 and 1938 run (not skipped) through the case runner, and those that pass on Windows hold the ratchet.

## Context

Found by BL-2546, which ported lib1933 to lib1938 as `LibAwsSigV4Request` in `Curl.Conformance.LibTest.UnitLibrary` (registered in `LibTestPrograms.Ported`, unit tested over a fake engine). Each case is skipped: `test1933 is skipped: Curl lacks the feature Debug`. The cases say "this relies on the Debug feature which allows tests to set the time" (`<features>SSL Debug crypto aws</features>`): upstream's debug build fixes the SigV4 signing time with `CURL_FORCETIME`. `UpstreamConformanceTests.cs` (around line 405) already stands in for Debug for the httpsig, HSTS-clock, alt-svc and HTTP/2 cases; extend that stand-in to a case naming the `aws` feature, and make sure the engine the libtest runner hands the program (the case's `Engine`) signs with the fixed time the stand-in uses for the curl tool, finding the project at fault (likely the runner in `Curl.Conformance.UnitLibrary` or the engine's SigV4 clock) and adding it to `touches` before any edit. Record the decision in an ADR if it is a behaviour choice.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=<n>` with `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` runs (does not skip) each of 1933 to 1938; each that passes on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, and each that fails is filed with the board script with its differing lines.
- [ ] No added case's output depends on the platform.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 5819e099 21e5ddeb c59fcab3 3d836d5e.
