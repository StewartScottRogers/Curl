---
id: BL-2627
title: Make upstream case 655 pass: call CURLOPT_RESOLVER_START_FUNCTION before resolving
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2627 — Make upstream case 655 pass: call CURLOPT_RESOLVER_START_FUNCTION before resolving

## Goal

Upstream case 655 (lib655, a resolver start callback that refuses, then one that allows) passes through the runner.

## Context

Found by BL-2534; the ports in `Curl.Conformance.LibTest.UnitLibrary` are unit tested and follow their C sources. Differences: 655 stdout `expected "hello\n", got the end`; stderr shows `Could not resolve host: failthis` and `curl_easy_perform should have returned CURLE_ABORTED_BY_CALLBACK but instead returned error 6`: the engine never calls the resolver start callback. Run each with `CURL_UPSTREAM_CASE=<n>` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine or Libcurl project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
