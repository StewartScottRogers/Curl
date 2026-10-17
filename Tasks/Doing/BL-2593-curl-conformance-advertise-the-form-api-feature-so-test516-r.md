---
id: BL-2593
title: Curl conformance: advertise the form-api feature so test516 runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2593 — Curl conformance: advertise the form-api feature so test516 runs

## Goal

The upstream case runner advertises the `form-api` feature, since `Libcurl` has `CURLOPT_HTTPPOST`, so test516 runs instead of being skipped.

## Context

Found by BL-2528 (lib516 port, a transfer with `CURLOPT_HTTPPOST` NULL). The runner reports `test516 is skipped: Curl lacks the feature form-api`. Check which other cases name `form-api` in `<features>` and run them too; a failing one is filed, not repaired. Run it with `CURL_UPSTREAM_CASE=<n>` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test in `Curl.Conformance.UnitTests`.

## Acceptance criteria

- [ ] The runner treats `form-api` as present, with a unit test in `Curl.Conformance.UnitTests`.
- [ ] test516 runs; if it passes on Windows it is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, otherwise it is filed with its differing lines.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1` reports 0 failing members for the changed library.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 9e3cf7e9 e71d5bc9 4272a6b9 c84701f0.
