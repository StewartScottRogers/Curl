---
id: BL-297
title: Re-send the -F multipart body when -L follows a 307 or 308
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-233]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-297 — Re-send the -F multipart body when -L follows a 307 or 308

## Goal

`curl -L -F a=b <url>` answered with a 307 or 308 sends the same multipart body again to the new location, byte for byte as curl 8.21.0 does.

## Context

- Found in BL-233 (2026-09-26). `RedirectFollower` (`Curl.Core.UnitLibrary`) rewinds only `ITransferContext.Upload`; an `HttpRequestOptions.Body` that is a `StreamBody` (the `-F` body `MultipartFormBodyBuilder` builds, whose files are streamed) is carried to the next hop already read to its end. 301/302/303 drop the POST body and are unaffected.
- Measure curl 8.21.0 (`/mingw64/bin/curl`) with `Record-CurlExchange.ps1 -Connections 2` answering 307 then 200, record the command and bytes in Notes, then pin them. Options: rebuild the body per hop, or rewind a seekable `StreamBody` as `RewindUpload` does.

## Acceptance criteria

- [ ] With an injected boundary, `-L -F a=b -F f=@file` against a 307 then a 200 sends the measured bytes on both connections.
- [ ] The same for 308.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

## Log

- 2026-09-26: Created.
