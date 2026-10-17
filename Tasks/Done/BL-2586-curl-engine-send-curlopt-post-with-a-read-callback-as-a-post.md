---
id: BL-2586
title: Curl.Engine: send CURLOPT_POST with a read callback as a POST of POSTFIELDSIZE bytes (test508)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2586 — Curl.Engine: send CURLOPT_POST with a read callback as a POST of POSTFIELDSIZE bytes (test508)

## Goal

A Libcurl handle with `CURLOPT_POST` 1, `CURLOPT_POSTFIELDSIZE` and a `CURLOPT_READFUNCTION` sends a `POST` whose body is read through the callback, as libcurl 8.22.0 does.

## Context

Found by BL-2527 running test508 through the ported `lib508`: `<verify><protocol>` differs at line 1, expected `POST /508 HTTP/1.1`, got `GET /508 HTTP/1.1`. The program posts 45 bytes one byte per read callback call, with `Content-Type: application/x-www-form-urlencoded`.

## Acceptance criteria

- [x] test508 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-17 (lane 2): the first acceptance criterion adds test508 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, which `touches` did not name, so it is added. BL-2528 (in Doing) touches the same ratchet file, so the task goes back to Backlog until they no longer overlap. No code was written.

- 2026-10-17 (lane 2): `ManagedEasyTransfer.ReadCallbackPostBodyOf` posts the read callback's bytes as a `StreamBody` (form-urlencoded, `POSTFIELDSIZE` as `Content-Length`, chunked when negative) for `CURLOPT_POST` with no `POSTFIELDS`, no `UPLOAD` and no MIME/form body. Chose to leave a POST with no read callback sending no body (libcurl would read stdin, which a library handle has none of). Measure-CodeQuality on Curl.Engine: 100% line and branch, no failing member after splitting the condition into `PostsWithoutFields`.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: afe076da 904b57ec 3d836d5e.
- 2026-10-17: Doing -> Backlog. touches Curl.Conformance.UnitTests/PassingUpstreamCases.txt, which BL-2528 in Doing also touches
- 2026-10-17: Backlog -> Doing. Criteria at claim: afe076da 904b57ec 3d836d5e.
- 2026-10-17: Doing -> Done. test508 passes and is on the ratchet; ManagedEasyTransfer posts a read-callback body for CURLOPT_POST
