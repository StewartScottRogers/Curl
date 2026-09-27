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
completed: 2026-09-26
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

- [x] `-F a=@-` and `-F a=<-` with piped input produce the body bytes and length measured on curl 8.21.0.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Measured 2026-09-26 on curl 8.21.0 (`/mingw64/bin/curl`, Schannel), request read off a loopback listener: `printf 'hello
world' | curl -s -o /dev/null -H Expect: -F <spec> http://127.0.0.1:<port>/`. Pinned in `Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderStandardInputTests.cs`:
  - `-F a=@-`: `Content-Disposition: form-data; name="a"; filename="-"`, **no** `Content-Type`, `Content-Length: 173`.
  - `-F a=<-`: no file name, no type, `Content-Length: 159`.
  - `-F a=@- -F b=<-`: the second part is empty, `Content-Length: 269`.
  - `-F "a=@-;filename=x.txt"`: `Content-Type: text/plain`, 203. `-F "a=@-;encoder=base64"`: `aGVsbG8Kd29ybGQ=`, 213.
- Why no Content-Type for `@-`: curl's `tool_formparse.c` buffers a piped standard input into memory and hands it to libcurl with `curl_mime_data_cb`, so libcurl's `Curl_mime_prepare_headers` sees a callback part, not a file part, and only a file name's extension gives it a type. `MultipartPartHeaders.FileFallbackContentType` does the same via `MultipartFormPart.ReadsStandardInput`.
- Choice (sensible default): the builder takes `Stream? standardInput = null` as an optional fourth constructor parameter and reads it whole, never closing it, so `Content-Length` stays known as curl's buffering keeps it. With no stream the part opens the path `-` through `IFileSystem` as before, so `Curl.Console` (out of this task's `touches`, and in Doing under BL-235) keeps its current behaviour rather than silently sending an empty part. Wiring it is BL-302.
- Not matched, recorded in BL-302: curl's behaviour for standard input redirected from a regular file with two `-` parts (declares both sizes, sends the second empty, exit 26), and for one `-F <-` sent to two URLs (second request short, exit 26). Both are measured there. No ADR: every output here is measured curl behaviour, not a design choice.
- Review (code-reviewer): no correctness bugs; its "silently empty" concern is why the no-stream fallback keeps the old file open.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. MultipartFormBodyBuilder sends -F @- and <- parts from injected standard input with curl 8.21.0's bytes and Content-Length
