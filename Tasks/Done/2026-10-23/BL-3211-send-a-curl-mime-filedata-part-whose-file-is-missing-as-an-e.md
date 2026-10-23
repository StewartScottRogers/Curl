---
id: BL-3211
title: Send a curl_mime_filedata part whose file is missing as an empty part, as libcurl 8.22.0 does
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-3210]
touches: [Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs, Curl.Engine.UnitLibrary/ManagedMimePartBuilder.cs, Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderMissingFileTests.cs, Curl.Engine.UnitTests/ManagedMimeMissingFileTests.cs, Curl.Api.UnitLibrary/Options/Http/EasyMimeFile.cs, Curl.Core.UnitLibrary/Multipart/MultipartFormPart.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Mime.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3211 — Send a curl_mime_filedata part whose file is missing as an empty part, as libcurl 8.22.0 does

## Goal

A libcurl `curl_mime_filedata` part whose file is missing makes the transfer send what libcurl 8.22.0 sends and end with its code (BL-3210's case 1).

## Context

Split from BL-3208. `SetFileData("nope.txt")` already returns 26 as upstream does, but upstream still sends the POST with that part empty; we fail with 26 before sending, from `MultipartFormBodyBuilder` (`ReadFailedMessage`, around lines 228, 341, 407). That builder serves the curl tool too, whose `-F a=@nope.txt` must keep failing with 26 before connecting (measured 2026-10-22, curl 8.22.0), so the libcurl path needs its own rule, carried on the part by `MultipartFormPartMapping` from `EasyMimeFile`.

## Acceptance criteria

- [x] `Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderMissingFileTests.cs` pins the body BL-3210 recorded for case 1, and the tool's `-F` missing file still fails with 26 before sending.
- [x] `Curl.Engine.UnitTests/ManagedMimeMissingFileTests.cs` pins the request and final code of a `CURLOPT_MIMEPOST` transfer with the missing file, as BL-3210 recorded.
- [x] Build clean, fast tests pass, Measure-CodeQuality.ps1 0 failing members for the changed libraries.

## Notes

- Added `EasyMimeFile.SendsEmptyWhenMissing` (set only by `ManagedMimePartBuilder.SetFileData`) and `MultipartFormPart.SendsEmptyWhenMissing`, so the tool's `-F` path still fails with 26. Touches extended with those two files and `ManagedEasyTransferTests.Mime.cs` (its old missing-file test pinned the pre-fix behaviour and was removed; no other Doing task names them). A directory still fails with 26. Engine tests live in a partial of `ManagedEasyTransferTests` to share its helpers. Measure-CodeQuality: Api, Core, Engine 0 failing members.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 733be95f 07e70ceb 491059de.
- 2026-10-22: Doing -> Done. Missing curl_mime_filedata file is sent as an empty part with code 0, as libcurl 8.22.0 does; tool -F still fails with 26
