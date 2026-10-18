---
id: BL-2681
title: Run upstream AWS SigV4 libtest cases 1955 to 1959 with the harness's Debug stand-in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2675]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2681 — Run upstream AWS SigV4 libtest cases 1955 to 1959 with the harness's Debug stand-in

## Goal

Upstream cases 1955, 1956, 1957, 1958 and 1959 run (not skipped) through the case runner, and those that pass on Windows hold the ratchet.

## Context

Found by BL-2547, which ported lib1955 to lib1959 as `LibAwsSigV4Headers` in `Curl.Conformance.LibTest.UnitLibrary` (registered in `LibTestPrograms.Ported`, unit tested over a fake engine). Each case is skipped: `test1955 is skipped: Curl lacks the feature Debug` (`<features>SSL Debug crypto aws</features>`, the Debug build fixing the SigV4 signing time). BL-2675 extends the harness's Debug stand-in in `UpstreamConformanceTests.cs` to the `aws` cases 1933 to 1938; once it is Done, run these five the same way. Any case that still fails is left off and filed with its differing lines.

## Acceptance criteria

- [x] `CURL_UPSTREAM_CASE=<n>` with `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` reports each of 1955 to 1959 as run, not skipped.
- [x] Every one of 1955 to 1959 that passes on Windows is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that fails is filed as a task with its test number and differing lines.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- No code change was needed: BL-2675 (838d93ddf) gave every case naming the `aws` feature the Debug stand-in and an epoch signing clock for the libtest engine, and already held 1955 to 1959 on the ratchet (`PassingUpstreamCases.txt` lines 1494 to 1498) among its 20 cases. Measured 2026-10-18: `CURL_UPSTREAM_CASE=<n>` with `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes (runs, not skipped) for each of 1955, 1956, 1957, 1958 and 1959; `Curl.Conformance.UnitTests` fast tests: 3476 passed, 0 failed. The cases are HTTP over loopback with signed headers only, no path or OS text, so they are platform-neutral (and already pass CI on Linux and macOS since BL-2675).

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 3ac4100f e3c7289e 4272a6b9.
- 2026-10-18: Doing -> Done. 1955 to 1959 run and pass; already held on the ratchet by BL-2675
