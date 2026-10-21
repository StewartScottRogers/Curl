---
id: BL-2643
title: Run upstream case 1543: a CURLOPT_CURLU handle parsed with CURLU_ALLOW_SPACE sends no request
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2643 — Run upstream case 1543: a CURLOPT_CURLU handle parsed with CURLU_ALLOW_SPACE sends no request

## Goal

Upstream case 1543 (lib1518 with `CURLOPT_CURLU`, a URL with spaces and a relative redirect) passes through the runner and holds the ratchet.

## Context

Found by BL-2538, which ported lib1518 (`Curl.Conformance.LibTest.UnitLibrary/Lib1518.cs`). For case 1543 the program parses `http://%HOSTIP:%HTTPPORT/ /with/ space/ /file` into a URL handle with `CURLU_ALLOW_SPACE`, hands it over with `CURLOPT_CURLU` and `CURLOPT_FOLLOWLOCATION`, performs, and prints `result`, `status`, `redirects`, `effectiveurl` and `redirecturl`. Through the runner on Windows no request reaches the server: `test1543 fails: <verify><protocol> differs at byte 0 (line 1): expected "GET /%20/with/%20space/%20/file HTTP/1.1\r\n", got the end`. Expected: the first request line `GET /%20/with/%20space/%20/file HTTP/1.1`, the redirect `GET /%20/with/%20space/15430002 HTTP/1.1`, and stdout `result 0`, `status 200`, `redirects 1`, `effectiveurl http://%HOSTIP:%HTTPPORT/%20/with/%20space/15430002`, `redirecturl blank`. Start at `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs` (its `AllowSpace` handling keeps raw spaces through `WithSpaces()`) and how Libcurl's `CURLOPT_CURLU` setter reads the handle's URL; upstream's handle answers the URL with each space as `%20`.

## Acceptance criteria

- [x] A URL handle set with `CURLU_ALLOW_SPACE` and handed over with `CURLOPT_CURLU` sends its path with each space as `%20`, with a focused unit test in the fixed library's `.UnitTests` project.
- [x] test1543 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: `UrlHandleOverride.Apply` read the handle's URL with no flags, so a `CURLU_ALLOW_SPACE` path kept its raw spaces and the request line broke. Fix: read the path with `CURLU_URLENCODE` on a duplicate of the handle, as curl 8.22.0's `parseurlandfillconn` does (`curl_url_get(uh, CURLUPART_PATH, ..., CURLU_URLENCODE)`), then take the whole URL; the application's handle is unchanged. Only the path is encoded, matching upstream (the query is read with no flags there).
- Unit test: `UrlHandleOverrideTests.Apply_HandleParsedAllowingSpaces_SendsEachPathSpaceAsPercent20`. test1543 passes through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; no platform-dependent output. Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 176cbd41 526cc4a9 3d836d5e.
- 2026-10-17: Doing -> Done. A CURLOPT_CURLU handle parsed with CURLU_ALLOW_SPACE sends its path spaces as %20; test1543 holds the ratchet
