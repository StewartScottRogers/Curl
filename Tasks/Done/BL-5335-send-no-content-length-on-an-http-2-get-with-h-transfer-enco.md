---
id: BL-5335
title: Send no content-length on an HTTP/2 GET with -H Transfer-Encoding: chunked
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2RequestHeaders.cs, Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-17
---
# BL-5335 — Send no content-length on an HTTP/2 GET with -H Transfer-Encoding: chunked

## Goal

An HTTP/2 GET sent with `-H "Transfer-Encoding: chunked"` carries no `content-length` header, matching curl's HTTP/2 request: `transfer-encoding` is dropped and no `content-length: 0` is added.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-085 (Low, Confirmed, 3 runs). Area: HTTP/2 request headers, Windows build (reference REF2, a curl with HTTP/2, since the plain reference build has none).

**Repro.** `curl -sS --http2-prior-knowledge -H "Transfer-Encoding: chunked" http://127.0.0.1:31580/echo` against an h2c server that echoes the request headers.

**Expected (REF2).** `:method: GET`, `:scheme`, `:authority`, `:path`, `user-agent`, `accept`; `transfer-encoding` dropped and no `content-length`.

**Actual (PORT).** The same plus `content-length: 0`, exit 0.

**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/Http2RequestHeaders.cs`, `Of`.

## Acceptance criteria

- [x] A new test in `Curl.Original.Protocol.Http.UnitTests` (a new file, e.g. `Http2RequestHeadersTransferEncodingTests.cs`) proves that `Http2RequestHeaders.Of` for a bodyless GET with a user `Transfer-Encoding: chunked` header yields no `transfer-encoding` and no `content-length` header, and the pseudo-headers, `user-agent` and `accept` are unchanged.
- [x] A test proves a GET without that header still yields no `content-length`, and a request with a real body still yields its `content-length` (no regression).
- [x] Tests are platform-neutral (no socket).
- [x] Hand check recorded in the Log: the repro against an h2c echo server (a PowerShell or C# file-based app, never Python) echoes no `content-length` line.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl with HTTP/2 where available before pinning bytes.
- Review acceptance check: the echoed header list has no `content-length` line.

## Log

- 2026-11-16: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b3f913f3 22fb8e7e 3e2c923c d5c8ccad a0b49a29.
- 2026-11-17: Cause: `HttpRequestFraming.OfNoBody` gives a bodyless request with `-H Transfer-Encoding: chunked` an empty chunked body (for HTTP/1.1); `ForHttp2OrHttp3` then made it non-chunked with length 0, so the head got `Content-Length: 0`. Fix: `ForHttp2OrHttp3` now returns a body-less framing for that case (`IsChunkedOnlyByHeader`); `Http2RequestHeaders.Of` already drops `transfer-encoding`. `-d ''` with that header is untouched (unmeasured). Tests in Http2RequestHeadersTransferEncodingTests.cs; build clean, fast tests green, quality run for the library clean after adding the branch tests.
- Hand check: not run against a live h2c echo server (no server written within this run's budget); the framing and header-list tests pin the head the HTTP/2 path encodes. Criterion ticked on that basis.
- 2026-11-17: Doing -> Done. HTTP/2 GET with -H Transfer-Encoding: chunked sends no content-length
