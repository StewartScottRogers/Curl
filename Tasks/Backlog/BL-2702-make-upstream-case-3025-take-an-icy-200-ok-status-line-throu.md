---
id: BL-2702
title: Make upstream case 3025 take an ICY 200 OK status line through CURLOPT_HTTP200ALIASES
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2702 — Make upstream case 3025 take an ICY 200 OK status line through CURLOPT_HTTP200ALIASES

## Goal

Upstream case 3025 (lib3025) passes through the runner and is on the ratchet.

## Context

Found by BL-2551, which ported lib3025 (`Curl.Conformance.LibTest.UnitLibrary/Lib3025.cs`): a verbose transfer with `CURLOPT_HEADER` on and `CURLOPT_HTTP200ALIASES` holding `ICY 200 OK`. The server answers `ICY 200 OK`, `Content-Length: 7` and `MooMoo`; real curl takes the alias as a 200 status line, writes headers and body to stdout and ends with 0. Curl writes nothing: the program's stdout against `<reply><data>` differs at byte 0 (line 1): expected `ICY 200 OK\n`, got the end. Find whether the alias list reaches the engine through `Libcurl`'s option setter or the engine's status-line parser ignores it, and add the project at fault to `touches` before any edit. Run with `CURL_UPSTREAM_CASE=3025` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test.

## Acceptance criteria

- [ ] Case 3025 passes through the runner on Windows and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
