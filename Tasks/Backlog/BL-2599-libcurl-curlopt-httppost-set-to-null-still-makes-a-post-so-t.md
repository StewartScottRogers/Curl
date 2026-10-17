---
id: BL-2599
title: Libcurl: CURLOPT_HTTPPOST set to NULL still makes a POST, so test516 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
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

- [ ] `CURLOPT_HTTPPOST` NULL makes the transfer a `POST` with `Content-Length: 0`, pinned by a unit test in `Libcurl.UnitTests`.
- [ ] test516 passes on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1` reports 0 failing members for `Libcurl`.

## Notes

## Log

- 2026-10-17: Created.
