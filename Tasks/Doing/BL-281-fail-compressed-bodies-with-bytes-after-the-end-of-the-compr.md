---
id: BL-281
title: Fail --compressed bodies with bytes after the end of the compressed stream as curl does
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-177]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-281 — Fail --compressed bodies with bytes after the end of the compressed stream as curl does

## Goal

With `--compressed`, a gzip, deflate or br body that carries bytes after the end of its compressed stream ends the transfer as curl 8.21.0 does: the decoded data written, then exit 23.

## Context

- Found while delivering BL-177 (2026-09-26). `HttpContentCodingDecoder` in `Curl.Protocol.Http.UnitLibrary` decodes with the BCL streams fed through `HttpContentInput`.
- Measured on curl 8.21.0 (mingw, the Windows reference, ADR-0018) with `Record-CurlExchange.ps1`, `-s -S --compressed`, `Content-Length` covering the stream plus `41 42`: for zlib-wrapped deflate and for br, stdout is `hello` and stderr is `curl: (23) Failed writing received data to disk/application`, exit 23. gzip with the same trailing `41 42` also exits 23 with the same message.
- Today: zlib and br silently drop the trailing bytes (exit 0); gzip lets `GZipStream` read them as a second member header, which throws and gives exit 61.
- Unmeasured: a second complete gzip member after the first. Measure it before deciding.

## Acceptance criteria

- [ ] For `deflate` (zlib), `br` and `gzip` bodies of `hello` followed by `41 42`, the handler writes `hello` and returns `CurlExitCode.WriteError` (23) with `Failed writing received data to disk/application`, pinned in a test fed with 1-byte, 7-byte and whole reads.
- [ ] The behaviour for a second complete gzip member is measured, recorded in Notes and pinned in a test.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
