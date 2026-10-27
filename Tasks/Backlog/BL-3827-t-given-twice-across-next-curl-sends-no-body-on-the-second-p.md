---
id: BL-3827
title: -T - given twice across --next: Curl sends no body on the second PUT where curl sends the stdin bytes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests/StandardInputUploadEarlyResponseTests.cs, Curl.Console.UnitTests/StandardInputUploadTwiceAcrossNextTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3827 — -T - given twice across --next: Curl sends no body on the second PUT where curl sends the stdin bytes

## Goal

`curl -s -T - URL/a --next -T - URL/b` with stdin `abc`, against a server that answers `200 OK` with `Connection: close` before reading the body, sends the same bytes on the wire as curl 8.22.0.

## Context

Found by BL-3546 (CGR-017) while measuring stdin repros with Record-CurlExchange.ps1 (-Response twice, Connection: close, -StandardInput abc). Real curl sends both PUT heads (chunked, Expect: 100-continue); the server answers /a before the body, curl does not consume stdin there, and the second PUT carries `3\r\nabc\r\n0\r\n\r\n`. Curl sends both heads and no body at all: stdin appears consumed (or the upload abandoned) by the first transfer. Exit codes and stdout match (0). The report notes `-T -` twice as not failing; this is a bytes-on-the-wire difference only. Where the stdin upload is read and abandoned on an early final response is not yet pinned: start in Curl.Protocol.Http.UnitLibrary and narrow `touches` once found.

## Acceptance criteria

- [ ] The command above, through real curl and Curl via Record-CurlExchange.ps1, gives the same exit code, stdout, stderr and request bytes; recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
