---
id: BL-4286
title: Extension GCS: send one HTTP/1.1 request over IConnection for the XML API
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4284]
touches: [Curl.Extension.GoogleCloudStorage.UnitLibrary/Http, Curl.Extension.GoogleCloudStorage.UnitTests/Http]
requirement: none
created: 2026-10-29
completed:
---
# BL-4286 — Extension GCS: send one HTTP/1.1 request over IConnection for the XML API

## Goal

The GoogleCloudStorage extension sends one HTTP/1.1 request at a time over an `IConnection` and returns the status, headers and a body stream framed by `Content-Length`, chunked or the close.

## Context

Split from BL-4125 for size, as BL-4199 was split from BL-4124. Copy the shape of `Curl.Extension.S3.UnitLibrary/Http` (an extension may not reference `Curl.Protocol.Http` or another extension, so it is written here; TLS comes from `ConnectTarget.UseTls` through the connector). Specification: RFC 9112 message framing, as the Cloud Storage XML API uses it. Needs BL-4284's project.

## Acceptance criteria

- [ ] An HTTP client connection in `Http/` writes a request (method, target, `Host`, headers, optional body with `Content-Length`) and reads the status line, headers and a body framed by `Content-Length`, chunked or the close, and says whether the connection persists; every failure is a named exception.
- [ ] Unit tests use recorded bytes over a fake `IConnection`, with no network.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
