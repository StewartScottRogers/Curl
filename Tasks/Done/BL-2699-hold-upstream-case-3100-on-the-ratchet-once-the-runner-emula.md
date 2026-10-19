---
id: BL-2699
title: Hold upstream case 3100 on the ratchet once the runner emulates the RTSP server
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2617, BL-2704]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2699 — Hold upstream case 3100 on the ratchet once the runner emulates the RTSP server

## Goal

Upstream case 3100 (lib3100, RTSP DESCRIBE with `CURLAUTH_ANY`) runs through the case runner and, when it passes, holds the ratchet.

## Context

Found by BL-2551, which ported lib3100 (`Curl.Conformance.LibTest.UnitLibrary/Lib3100.cs`). Measured on Windows: `test3100 is skipped: the harness does not emulate the rtsp server`. BL-2617 emulates that server. Once it is Done, run the case with `CURL_UPSTREAM_CASE=3100` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test. The case expects a first `DESCRIBE` without credentials and a second with `Authorization: Basic` for `user:password` after the server's 401.

## Acceptance criteria

- [x] Case 3100 runs through the runner on Windows without being skipped.
- [x] If it passes it is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; if it fails, a task is filed with its differing lines.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: Measured on Windows with CURL_UPSTREAM_CASE=3100: test3100 runs (no longer skipped) but fails, `<verify><protocol>` line 1 expected `DESCRIBE rtsp://127.0.0.1:8996/3100 RTSP/1.0`, got `OPTIONS * RTSP/1.0`. Same cause as BL-2704 (RtspProtocolHandler ignores CURLOPT_RTSP_REQUEST). Requeued behind BL-2704; then add 3100 to the ratchet if it passes (it also needs the 401 Basic retry with CURLAUTH_ANY).
- 2026-10-18 (second claim, BL-2704 Done): case 3100 runs on Windows, not skipped, and now fails later: `<verify><protocol>` differs at byte 82 (line 5) - the first DESCRIBE (lines 1-4) matches, but Curl never resends DESCRIBE with `CSeq: 2` and `Authorization: Basic dXNlcjpwYXNzd29yZA==` after the 401. RtspProtocolHandler has only pre-emptive auth. Filed BL-2788 with the differing lines; it adds 3100 to the ratchet when it passes. Not added here, since it fails.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: d919f019 4df473d9 3d836d5e.
- 2026-10-18: Doing -> Backlog. Needs BL-2704: RTSP handler ignores CURLOPT_RTSP_REQUEST, case 3100 sends OPTIONS * not DESCRIBE
- 2026-10-18: Backlog -> Doing. Criteria at claim: d919f019 4df473d9 3d836d5e.
- 2026-10-18: Doing -> Done. Case 3100 runs through the runner on Windows; it fails on the missing 401 retry, filed as BL-2788
