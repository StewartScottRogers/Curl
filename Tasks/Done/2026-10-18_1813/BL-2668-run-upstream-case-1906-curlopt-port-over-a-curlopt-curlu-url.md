---
id: BL-2668
title: Run upstream case 1906: CURLOPT_PORT over a CURLOPT_CURLU URL, then CURLOPT_PORT 0 back to the URL's port
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2606]
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2668 â€” Run upstream case 1906: CURLOPT_PORT over a CURLOPT_CURLU URL, then CURLOPT_PORT 0 back to the URL's port

## Goal

Upstream case 1906 (lib1906, `CURLOPT_CURLU` with `CURLOPT_PORT` 1, then `CURLOPT_PORT` 0) passes through the runner and is on the ratchet.

## Context

Found by BL-2544, which ported lib1906 (`Curl.Conformance.LibTest.UnitLibrary/Lib1906.cs`). The first perform must fail with `CURLE_COULDNT_CONNECT` (port 1), the `CURLU` handle must keep its own URL, and the second perform with `CURLOPT_PORT` 0 must go back to the URL's port and send `GET /1906 HTTP/1.1`. Measured on Windows: `<verify><protocol> differs at byte 0 (line 1): expected "GET /1906 HTTP/1.1\r\n", got the end` - no request reaches the server. BL-2606 makes `CURLOPT_PORT` apply at all; this task is what is left after it (0 meaning the URL's port, and the `CURLU` handle left unchanged).

## Acceptance criteria

- [x] `CURLOPT_PORT` 0 after a non-zero port makes a `Libcurl` handle connect to the URL's own port again, and a `CURLOPT_CURLU` handle's URL is not changed by a perform, with focused unit tests in the fixed library's own `.UnitTests`.
- [x] Case 1906 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: No product change was needed. BL-2628's `UrlHandleOverride` reads the `CURLU` handle's URL into a copy at each perform, and BL-2606's `UrlPortOverride` applies only a non-zero `CURLOPT_PORT`, so case 1906 already passed through the runner (`CURL_UPSTREAM_CASE=1906`) when this run measured it. Added the focused test `CurlEasyTests.Perform_PortOptionZeroAfterAPortOverAUrlHandle_TransfersTheUrlsOwnPortAndLeavesTheHandleUnchanged` in `Libcurl.UnitTests` and held 1906 on the ratchet. The case dials port 1, which is refused on every platform (`CURLE_COULDNT_CONNECT`), and checks only the protocol bytes, so its result does not depend on the platform. Measure-CodeQuality was not run because no production code changed.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 6540aefd 3f84cb54 3d836d5e.
- 2026-10-18: Doing -> Done. CURLOPT_PORT 0 after a port over a CURLU handle transfers the URL's own port with the handle unchanged; case 1906 held on the ratchet
