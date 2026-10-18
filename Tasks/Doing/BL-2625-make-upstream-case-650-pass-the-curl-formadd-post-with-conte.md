---
id: BL-2625
title: Make upstream case 650 pass: the curl_formadd post with content headers, arrays, files and stdin sends nothing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2625 — Make upstream case 650 pass: the curl_formadd post with content headers, arrays, files and stdin sends nothing

## Goal

Upstream case 650 (lib650, every curl_formadd feature, followed 301 as POST) passes through the runner.

## Context

Found by BL-2534; the ports in `Curl.Conformance.LibTest.UnitLibrary` are unit tested and follow their C sources. Differences: 650 `<verify><protocol> differs at byte 0 (line 1): expected "POST /650 HTTP/1.1\r\n", got the end`, with empty stdout and stderr, so every curl_formadd succeeded and the perform failed before sending (suspects: CURLFORM_FILE "-" from stdin, CURLFORM_FILECONTENT, the multi-file field, or CURLFORM_CONTENTHEADER). Run each with `CURL_UPSTREAM_CASE=<n>` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine or Libcurl project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
