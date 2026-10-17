---
id: BL-2591
title: Libcurl: send CURLOPT_NOBODY set after CURLOPT_POST as a HEAD (test514)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2591 — Libcurl: send CURLOPT_NOBODY set after CURLOPT_POST as a HEAD (test514)

## Goal

A Libcurl handle given `CURLOPT_POSTFIELDS`, `CURLOPT_POSTFIELDSIZE` and `CURLOPT_POST` and then `CURLOPT_NOBODY` 1 sends a HEAD with no body, so upstream test514 passes.

## Context

Found by BL-2528 (lib514 port). curl 8.22.0 sends `HEAD /514 HTTP/1.1`; Curl sends `POST /514 HTTP/1.1`: `test514 fails: <verify><protocol> differs at byte 0 (line 1)`. In libcurl, setting `CURLOPT_NOBODY` 1 switches the method to HEAD over an earlier POST. Run it with `CURL_UPSTREAM_CASE=<n>` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test in `Curl.Conformance.UnitTests`.

## Acceptance criteria

- [x] A focused unit test in `Curl.Engine.UnitTests` shows `CURLOPT_NOBODY` set after the POST options sends HEAD with no body.
- [x] test514 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1` reports 0 failing members for the changed library.

## Notes

- 2026-10-17 (lane 2): Added `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` to `touches`: acceptance criterion 2 adds test514 to it. BL-2595, in Doing, holds that file, so the task goes back to Backlog before any code; the board offers it again once BL-2595 no longer overlaps.

- 2026-10-17 (lane 2, run 2): BL-2595 is Done, so the overlap is gone. Fix in `HttpRequestOptionsMapping.RequestBodyOf`: `NoBody` drops the PostFields and form body, so the HTTP framing picks HEAD. Libcurl already clears `NoBody` when POST or POSTFIELDS is set after NOBODY, so the last option set wins, as libcurl. The curl tool refuses `-I` with a body, so the CLI path is unchanged. test514 passes on Windows and is on the ratchet; it is HTTP only, with no path or OS text, so it is platform neutral. Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: ad030c9b 995038b4 4272a6b9 c84701f0.
- 2026-10-17: Doing -> Backlog. Touches Curl.Conformance.UnitTests/PassingUpstreamCases.txt, which BL-2595 (in Doing) holds; requeued until they no longer overlap.
- 2026-10-17: Backlog -> Doing. Criteria at claim: ad030c9b 995038b4 4272a6b9 c84701f0.
- 2026-10-17: Doing -> Done. A Libcurl handle given CURLOPT_NOBODY after CURLOPT_POST sends a HEAD with no body; test514 passes and is on the ratchet
