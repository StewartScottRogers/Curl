---
id: BL-275
title: Send -F parts read from standard input (@- and <-) in the multipart body
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-205]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-275 — Send -F parts read from standard input (@- and <-) in the multipart body

## Goal

`MultipartFormBodyBuilder` sends `-F name=@-` and `-F name=<-` parts from standard input as curl 8.21.0 does, with the body length it measured.

## Context

- Found while delivering BL-205 (2026-09-26): BL-189 names `-` as standard input for `@` and `<` parts, and `MultipartFormPart` (ADR-0027) only opens files through `IFileSystem`.
- Read curl 8.21.0's `src/tool_formparse.c` for how it takes standard input for these parts (into memory or streamed); measure the headers (the file name of `@-`, the content type) and whether the body is sent with `Content-Length` or chunked.
- Inject standard input as a `Stream`; never read the real console in a test.
- Where a criterion says *measured*, run curl 8.21.0 (`/mingw64/bin/curl`) with `Record-CurlExchange.ps1`, record the command and bytes in `Notes`, then pin them in a test.

## Acceptance criteria

- [ ] `-F a=@-` and `-F a=<-` with piped input produce the body bytes and length measured on curl 8.21.0.
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

## Log

- 2026-09-26: Created.
