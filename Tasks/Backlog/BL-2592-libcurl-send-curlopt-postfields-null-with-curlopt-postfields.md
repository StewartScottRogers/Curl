---
id: BL-2592
title: Libcurl: send CURLOPT_POSTFIELDS NULL with CURLOPT_POSTFIELDSIZE 0 as a zero-byte POST (test515)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2592 — Libcurl: send CURLOPT_POSTFIELDS NULL with CURLOPT_POSTFIELDSIZE 0 as a zero-byte POST (test515)

## Goal

A Libcurl handle given `CURLOPT_POSTFIELDS` NULL and `CURLOPT_POSTFIELDSIZE` 0 sends a zero-byte POST, so upstream test515 passes.

## Context

Found by BL-2528 (lib515 port). curl 8.22.0 sends `POST /515 HTTP/1.1` with `Content-Length: 0`; Curl sends `GET /515 HTTP/1.1`: `test515 fails: <verify><protocol> differs at byte 0 (line 1)`. Related but separate: BL-2586 (POST with a read callback sent as GET: test508, and also test510 and test565). Run it with `CURL_UPSTREAM_CASE=<n>` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test in `Curl.Conformance.UnitTests`.

## Acceptance criteria

- [ ] A focused unit test in `Curl.Engine.UnitTests` shows `CURLOPT_POSTFIELDS` NULL with `CURLOPT_POSTFIELDSIZE` 0 sends POST with `Content-Length: 0`.
- [ ] test515 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1` reports 0 failing members for the changed library.

## Notes

- 2026-10-17 (lane 2): The second criterion adds test515 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, which `touches` did not name; added it. BL-2595 (in Doing) holds that file, so the task is requeued before any code edit until BL-2595 no longer overlaps.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 1577eda6 580ce1a6 4272a6b9 c84701f0.
- 2026-10-17: Doing -> Backlog. Curl.Conformance.UnitTests/PassingUpstreamCases.txt is held by BL-2595 in Doing; requeued before any code edit
