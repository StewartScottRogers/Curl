---
id: BL-2699
title: Hold upstream case 3100 on the ratchet once the runner emulates the RTSP server
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2617]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2699 — Hold upstream case 3100 on the ratchet once the runner emulates the RTSP server

## Goal

Upstream case 3100 (lib3100, RTSP DESCRIBE with `CURLAUTH_ANY`) runs through the case runner and, when it passes, holds the ratchet.

## Context

Found by BL-2551, which ported lib3100 (`Curl.Conformance.LibTest.UnitLibrary/Lib3100.cs`). Measured on Windows: `test3100 is skipped: the harness does not emulate the rtsp server`. BL-2617 emulates that server. Once it is Done, run the case with `CURL_UPSTREAM_CASE=3100` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test. The case expects a first `DESCRIBE` without credentials and a second with `Authorization: Basic` for `user:password` after the server's 401.

## Acceptance criteria

- [ ] Case 3100 runs through the runner on Windows without being skipped.
- [ ] If it passes it is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; if it fails, a task is filed with its differing lines.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
