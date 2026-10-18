---
id: BL-2599
title: Libcurl: CURLOPT_HTTPPOST set to NULL still makes a POST, so test516 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.LibTest.UnitLibrary/Lib516.cs]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2599 — Libcurl: CURLOPT_HTTPPOST set to NULL still makes a POST, so test516 passes

## Goal

An easy handle given `CURLOPT_HTTPPOST` NULL sends `POST` with `Content-Length: 0`, as libcurl does, so upstream test516 passes.

## Context

Found by BL-2593, which made the conformance runner report `form-api`, so test516 (`lib516`, `Curl.Conformance.LibTest.UnitLibrary/Lib516.cs`) now runs. It fails:

```
<verify><protocol> differs at byte 0 (line 1): expected "POST /516 HTTP/1.1\r\n", got "GET /516 HTTP/1.1\r\n"
- 1: "POST /516 HTTP/1.1\r\n"
- 2: "Host: 127.0.0.1:8990\r\n"
- 3: "Accept: */*\r\n"
- 4: "Content-Length: 0\r\n"
+ 1: "GET /516 HTTP/1.1\r\n"
+ 2: "Host: 127.0.0.1:8990\r\n"
+ 3: "Accept: */*\r\n"
  5: "\r\n"
```

Upstream's `setopt.c` (curl 8.22.0), `CURLOPT_HTTPPOST`, sets `data->set.method = HTTPREQ_POST_FORM` and `opt_no_body = FALSE` whatever the pointer, so a NULL form still POSTs with an empty body. `Libcurl/Mime/Form/EasyOptionSetters.HttpPost.cs` `SetHttpPost` only stores the (null) body. Run the case with `CURL_UPSTREAM_CASE=516` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` (`TestCategory=Manual`) in `Curl.Conformance.UnitTests`.

## Acceptance criteria

- [x] `CURLOPT_HTTPPOST` NULL makes the transfer a `POST` with `Content-Length: 0`, pinned by a unit test in `Libcurl.UnitTests`.
- [x] test516 passes on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1` reports 0 failing members for `Libcurl`.

## Notes

- Touches widened to `Curl.Engine.UnitLibrary`, `Curl.Engine.UnitTests` and `Curl.Conformance.LibTest.UnitLibrary/Lib516.cs` (no other task in Doing on origin/work/dark-factory named them): the engine sends a POST only for a body, so a NULL form needs the engine to send an empty `CURLOPT_HTTPPOST` form as an empty body; Lib516's summary said it "makes no POST", which is false.
- Decision (default taken): Libcurl stores a NULL `CURLOPT_HTTPPOST` as an empty form (an `EasyMimeBody` with no parts), which is what a NULL `struct curl_httppost *` is in C (an empty list), and clears `CURLOPT_NOBODY` for any form, as upstream's `setopt.c` does. `MimePostForm.BuildAsync` sends an empty `CURLOPT_HTTPPOST` form as a zero-length body with no `Content-Type`, as upstream's `Curl_getformdata` leaves the part `MIMEKIND_NONE`. An empty `CURLOPT_MIMEPOST` is left as it was.
- Measured: test516 passes (`CURL_UPSTREAM_CASE=516`); fast tests green; `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members. The case is plain HTTP over loopback, so its output does not depend on the platform.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 0b872f9d 8f5ef050 4272a6b9 93d914d5.
- 2026-10-17: Doing -> Done. CURLOPT_HTTPPOST NULL now posts with Content-Length: 0; test516 on the ratchet
