---
id: BL-2586
title: Curl.Engine: send CURLOPT_POST with a read callback as a POST of POSTFIELDSIZE bytes (test508)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2586 — Curl.Engine: send CURLOPT_POST with a read callback as a POST of POSTFIELDSIZE bytes (test508)

## Goal

A Libcurl handle with `CURLOPT_POST` 1, `CURLOPT_POSTFIELDSIZE` and a `CURLOPT_READFUNCTION` sends a `POST` whose body is read through the callback, as libcurl 8.22.0 does.

## Context

Found by BL-2527 running test508 through the ported `lib508`: `<verify><protocol>` differs at line 1, expected `POST /508 HTTP/1.1`, got `GET /508 HTTP/1.1`. The program posts 45 bytes one byte per read callback call, with `Content-Type: application/x-www-form-urlencoded`.

## Acceptance criteria

- [ ] test508 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
