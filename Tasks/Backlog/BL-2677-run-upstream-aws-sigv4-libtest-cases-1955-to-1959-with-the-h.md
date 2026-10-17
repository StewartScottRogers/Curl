---
id: BL-2677
title: Run upstream AWS SigV4 libtest cases 1955 to 1959 with the harness's Debug stand-in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2675]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2677 — Run upstream AWS SigV4 libtest cases 1955 to 1959 with the harness's Debug stand-in

## Goal

Upstream cases 1955, 1956, 1957, 1958 and 1959 run (not skipped) through the case runner, and those that pass on Windows hold the ratchet.

## Context

Found by BL-2547, which ported lib1955 to lib1959 as `LibAwsSigV4Headers` in `Curl.Conformance.LibTest.UnitLibrary` (registered in `LibTestPrograms.Ported`, unit tested over a fake engine). Each case is skipped: `test1955 is skipped: Curl lacks the feature Debug` (`<features>SSL Debug crypto aws</features>`, the Debug build fixing the SigV4 signing time). BL-2675 extends the harness's Debug stand-in in `UpstreamConformanceTests.cs` to the `aws` cases 1933 to 1938; once it is Done, run these five the same way. Any case that still fails is left off and filed with its differing lines.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=<n>` with `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` reports each of 1955 to 1959 as run, not skipped.
- [ ] Every one of 1955 to 1959 that passes on Windows is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that fails is filed as a task with its test number and differing lines.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
