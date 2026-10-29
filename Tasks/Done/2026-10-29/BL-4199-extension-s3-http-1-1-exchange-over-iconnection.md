---
id: BL-4199
title: Extension: S3 HTTP/1.1 exchange over IConnection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4197]
touches: [Curl.Extension.S3.UnitLibrary/Http, Curl.Extension.S3.UnitTests/Http, Curl.Extension.S3.UnitLibrary/Curl.Extension.S3.UnitLibrary.csproj, Curl.Extension.S3.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4199 — Extension: S3 HTTP/1.1 exchange over IConnection

## Goal

`Curl.Extension.S3.UnitLibrary` sends one HTTP/1.1 request over an `IConnection` and reads the response's status, headers and body (Content-Length, chunked, or until close), so the `s3://` handler can talk to S3, MinIO or R2 without referencing `Curl.Protocol.Http`.

## Context

Split from BL-4124 for size. The project is created by BL-4197. An extension never references a `Curl.Protocol.*` library (ADR-0999), so this small client is written here per BL-4124's Notes. RFC 9112 §3 (request line), §4 (status line), §5 (fields), §6.3 (message body length), §7.1 (chunked). Request body streamed from a `Stream` with a known length; response body exposed as a stream so a large object is not buffered. Keep-alive so a multipart upload can reuse the connection. `Expect: 100-continue` is left out (record it). Code in `Http/`; tests use a fake `IConnection` with recorded bytes.

## Acceptance criteria

- [x] The exchange exists under `Curl.Extension.S3.UnitLibrary/Http/` and writes the request bytes and reads Content-Length, chunked and close-delimited responses, with unit tests over a fake connection citing the RFC 9112 section for each.
- [x] A malformed status line or a connection closed early yields a typed failure, not an unhandled exception.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Touches widened with the library's `.csproj` (it needs a reference to `Curl.Protocol.Abstractions`
  for `IConnection`) and its `CLAUDE.md` (which said `Http/` was still to come); no task in Doing on
  `origin/work/dark-factory` names either.
- Done directly rather than through the whole `/feature` pipeline, to stay inside the run's cost cap;
  the design is recorded in ADR-1018.
- Defaults taken: the caller, not the client, calls `MarkReusable` once the body is read (only it knows
  when); obs-fold is rejected; a `Content-Length` list is invalid; a line over 64 KiB fails; chunked
  trailers are discarded; `Expect: 100-continue` is left out, so a 1xx comes back as a response with
  no body.
- Platform-neutral: the tests use in-memory bytes only, with no paths, OS errors or certificates.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3c10a2b0 d089179a 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. The S3 extension sends one HTTP/1.1 request over an IConnection and reads Content-Length, chunked and close-delimited responses, with typed S3HttpException failures
