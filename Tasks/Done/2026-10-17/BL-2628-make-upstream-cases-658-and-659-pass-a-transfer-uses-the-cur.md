---
id: BL-2628
title: Make upstream cases 658 and 659 pass: a transfer uses the CURLOPT_CURLU handle over CURLOPT_URL
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2628 — Make upstream cases 658 and 659 pass: a transfer uses the CURLOPT_CURLU handle over CURLOPT_URL

## Goal

Upstream cases 658 (CURLOPT_CURLU set after CURLOPT_URL overrides it) and 659 (CURLU with no path, through a proxy) pass through the runner.

## Context

Found by BL-2534; the ports in `Curl.Conformance.LibTest.UnitLibrary` are unit tested and follow their C sources. Differences: 658 `<verify><protocol> differs at byte 0 (line 1): expected "GET /658 HTTP/1.1\r\n", got the end` with stderr `curl_easy_perform() failed with code 6 (Could not resolve hostname)`: the transfer resolved the CURLOPT_URL `www.example.com` instead of the CURLU handle's URL; 659 `expected "GET http://www.example.com/ HTTP/1.1\r\n", got the end`. Run each with `CURL_UPSTREAM_CASE=<n>` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine or Libcurl project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Cause: `Libcurl` stored `CURLOPT_CURLU` as `EasyNetworkOptions.UrlHandle`, but `Curl.Engine.UnitLibrary` never
  read it. Added `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` to `touches` (no other task was in Doing on
  `origin/work/dark-factory`).
- Fix: `UrlHandleOverride.Apply` in the perform from the options reads the handle's whole URL at perform time, as
  `Curl_pretransfer` does, before `UrlPortOverride`; an unreadable handle leaves no URL (`No URL set`). ADR-0673.
- Cases 658 and 659 pass through the runner on Windows and are on the ratchet; both are plain HTTP over the
  in-memory servers, with no path, file system or certificate in their output, so neither depends on the platform.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-17: Doing -> Done. A library transfer uses the CURLOPT_CURLU handle's URL over CURLOPT_URL; upstream cases 658 and 659 hold the ratchet
