---
id: BL-5335
title: Send no content-length on an HTTP/2 GET with -H Transfer-Encoding: chunked
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2RequestHeaders.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpRequestFraming.cs, Curl.Original.Protocol.Http.UnitTests]
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
- Touches widened to HttpRequestFraming.cs: the cause is in `ForHttp2OrHttp3` (no task in Doing on work/dark-factory names it).
- Resumed lane 8's branch (factory/BL-5335-lane-8-20261116-204618). Its `IsChunkedOnlyByHeader` property pattern left an uncovered branch; it now checks `ReferenceEquals(Body, HeaderOnlyChunkedBody)`, the one empty body `OfNoBody` gives a header-only chunked request, so it has no branch. `-d ''` with the header gets a body of its own and keeps its framing (unmeasured, unchanged).

## Log

- 2026-11-16: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b3f913f3 22fb8e7e 3e2c923c d5c8ccad a0b49a29.
- 2026-11-17: Doing -> Backlog. Lane 8 could not integrate: quality gates missed by 1 member(s) of files it changed: HttpRequestFraming.get_IsChunkedOnlyByHeader() (Curl.Original.Protocol.Http.UnitLibrary/HttpRequestFraming.cs: branch); the repair run's commit failed. The work is on branch factory/BL-5335-lane-8-20261116-204618; start with git cherry-pick --no-commit factory/BL-5335-lane-8-20261116-204618 and fix it.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b3f913f3 22fb8e7e 3e2c923c d5c8ccad a0b49a29.
- 2026-11-17: Cause: `OfNoBody` gives a bodyless request with `-H Transfer-Encoding: chunked` an empty chunked body for HTTP/1.1, and `ForHttp2OrHttp3` turned it into a length-0 body, so the HTTP/2 head got `content-length: 0`. `ForHttp2OrHttp3` now gives that request no body. Tests in Http2RequestHeadersTransferEncodingTests.cs.
- 2026-11-17: Hand check: `Record-CurlExchange.ps1 -Http2Origin prior-knowledge` with `--http2-prior-knowledge -sS -H "Transfer-Encoding: chunked" http://127.0.0.1:31580/echo` through Curl: HEADERS stream 1 flags 0x05 carried `:method: GET`, `:scheme: http`, `:authority`, `:path: /echo`, `user-agent`, `accept` and no `content-length` or `transfer-encoding`; exit 0.
- 2026-11-17: dotnet build clean, fast tests green, Measure-CodeQuality for Curl.Original.Protocol.Http.UnitLibrary: 0 failing members.
- 2026-11-17: Doing -> Done. An HTTP/2 GET with -H Transfer-Encoding: chunked sends no content-length
