---
id: BL-3211
title: Send a curl_mime_filedata part whose file is missing as an empty part, as libcurl 8.22.0 does
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-3210]
touches: [Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs, Curl.Engine.UnitLibrary/ManagedMimePartBuilder.cs, Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderMissingFileTests.cs, Curl.Engine.UnitTests/ManagedMimeMissingFileTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3211 — Send a curl_mime_filedata part whose file is missing as an empty part, as libcurl 8.22.0 does

## Goal

A libcurl `curl_mime_filedata` part whose file is missing makes the transfer send what libcurl 8.22.0 sends and end with its code (BL-3210's case 1).

## Context

Split from BL-3208. `SetFileData("nope.txt")` already returns 26 as upstream does, but upstream still sends the POST with that part empty; we fail with 26 before sending, from `MultipartFormBodyBuilder` (`ReadFailedMessage`, around lines 228, 341, 407). That builder serves the curl tool too, whose `-F a=@nope.txt` must keep failing with 26 before connecting (measured 2026-10-22, curl 8.22.0), so the libcurl path needs its own rule, carried on the part by `MultipartFormPartMapping` from `EasyMimeFile`.

## Acceptance criteria

- [ ] `Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderMissingFileTests.cs` pins the body BL-3210 recorded for case 1, and the tool's `-F` missing file still fails with 26 before sending.
- [ ] `Curl.Engine.UnitTests/ManagedMimeMissingFileTests.cs` pins the request and final code of a `CURLOPT_MIMEPOST` transfer with the missing file, as BL-3210 recorded.
- [ ] Build clean, fast tests pass, Measure-CodeQuality.ps1 0 failing members for the changed libraries.

## Notes

## Log

- 2026-10-22: Created.
