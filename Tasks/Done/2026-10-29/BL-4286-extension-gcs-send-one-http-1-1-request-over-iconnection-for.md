---
id: BL-4286
title: Extension GCS: send one HTTP/1.1 request over IConnection for the XML API
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4284]
touches: [Curl.Extension.GoogleCloudStorage.UnitLibrary/Http, Curl.Extension.GoogleCloudStorage.UnitTests/Http, Curl.Extension.GoogleCloudStorage.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4286 — Extension GCS: send one HTTP/1.1 request over IConnection for the XML API

## Goal

The GoogleCloudStorage extension sends one HTTP/1.1 request at a time over an `IConnection` and returns the status, headers and a body stream framed by `Content-Length`, chunked or the close.

## Context

Split from BL-4125 for size, as BL-4199 was split from BL-4124. Copy the shape of `Curl.Extension.S3.UnitLibrary/Http` (an extension may not reference `Curl.Protocol.Http` or another extension, so it is written here; TLS comes from `ConnectTarget.UseTls` through the connector). Specification: RFC 9112 message framing, as the Cloud Storage XML API uses it. Needs BL-4284's project.

## Acceptance criteria

- [x] An HTTP client connection in `Http/` writes a request (method, target, `Host`, headers, optional body with `Content-Length`) and reads the status line, headers and a body framed by `Content-Length`, chunked or the close, and says whether the connection persists; every failure is a named exception.
- [x] Unit tests use recorded bytes over a fake `IConnection`, with no network.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added `Curl.Extension.GoogleCloudStorage.UnitLibrary/CLAUDE.md` to touches: the project folder's own description now names `Http/`, so the document stays true of the code; no other task in Doing names it.
- The types mirror `Curl.Extension.S3.UnitLibrary/Http` with a `Gcs` prefix (the sensible default the task names); an extension cannot reference another extension.
- Measure-CodeQuality.ps1 -Library Curl.Extension.GoogleCloudStorage.UnitLibrary: 0 failing members. Full build clean, fast tests green on Windows; the tests use only in-memory recorded bytes, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: ff25c198 1a0f684d 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. Gcs HTTP/1.1 client connection over IConnection with Content-Length, chunked and close framing; quality gates met, fast tests green.
