---
id: BL-4195
title: Extension: S3 HTTP/1.1 exchange over IConnection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4193]
touches: [Curl.Extension.S3.UnitLibrary/Http, Curl.Extension.S3.UnitTests/Http]
requirement: none
created: 2026-10-29
completed:
---
# BL-4195 — Extension: S3 HTTP/1.1 exchange over IConnection

## Goal

`Curl.Extension.S3.UnitLibrary` sends one HTTP/1.1 request over an `IConnection` and reads the response's status, headers and body (Content-Length, chunked, or until close), so the `s3://` handler can talk to S3, MinIO or R2 without referencing `Curl.Protocol.Http`.

## Context

Split from BL-4124 for size. The project is created by BL-4193. An extension never references a `Curl.Protocol.*` library (ADR-0999), so this small client is written here per BL-4124's Notes. RFC 9112 §3 (request line), §4 (status line), §5 (fields), §6.3 (message body length), §7.1 (chunked). Request body streamed from a `Stream` with a known length; response body exposed as a stream so a large object is not buffered. Keep-alive so a multipart upload can reuse the connection. `Expect: 100-continue` is left out (record it). Code in `Http/`; tests use a fake `IConnection` with recorded bytes.

## Acceptance criteria

- [ ] The exchange exists under `Curl.Extension.S3.UnitLibrary/Http/` and writes the request bytes and reads Content-Length, chunked and close-delimited responses, with unit tests over a fake connection citing the RFC 9112 section for each.
- [ ] A malformed status line or a connection closed early yields a typed failure, not an unhandled exception.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
