---
id: BL-2687
title: Run upstream case 1978 with the harness's Debug stand-in for AWS SigV4
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2675]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2687 — Run upstream case 1978 with the harness's Debug stand-in for AWS SigV4

## Goal

Upstream case 1978 runs (not skipped) through the case runner and, when it passes on Windows, holds the ratchet.

## Context

Found by BL-2549, which ported lib1978 (`Curl.Conformance.LibTest.UnitLibrary/Lib1978.cs`: an empty upload signed `aws:amz:us-east-1:s3` with nineteen duplicate, differently cased and valueless headers the signature must merge, then the same upload unsigned). Measured on Windows: `test1978 is skipped: Curl lacks the feature Debug`. The case needs the same Debug stand-in (fixed SigV4 signing time) BL-2675 adds for cases 1933 to 1938; once that lands, run 1978 and, if it fails, find the project at fault (likely the SigV4 header merging) and add it to `touches` before any edit.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=1978` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` is not skipped.
- [ ] Case 1978 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` when it passes, or its differing line is filed as a task.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
