---
id: BL-658
title: Send an HTTP/2 request and read its response on a stream
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-656, BL-657, BL-668]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-658 — Send an HTTP/2 request and read its response on a stream

## Goal

When a connection speaks HTTP/2, the HTTP handler sends the request as HEADERS (pseudo-headers and header order as curl 8.21.0 sends them, lower-cased names) and DATA frames, reads the response's HEADERS, DATA and trailers, and feeds the same output, `-i`/`-D`, `-f`, redirect, auth, cookie and progress paths as HTTP/1.1, with stream errors mapped to curl's exits (92 `CURLE_HTTP2_STREAM`, 16 `CURLE_HTTP2`).

## Context

- Conformance audit 2026-09-28, row 32. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): HTTP/2 is offered on every platform. Builds on BL-656 (HPACK) and BL-657 (frames) in `Curl.Http2.UnitLibrary`: add that `ProjectReference` to `Curl.Protocol.Http.UnitLibrary` (allowed by BL-668) and amend `Curl.Protocol.Http.UnitLibrary/CLAUDE.md` to name it. Keep the request/response plumbing version-neutral so the HTTP/3 path (BL-731) reuses it.
- The handler's HTTP/1.1 path (`Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` and its request writer and head reader) is the model; the version choice itself (ALPN, prior knowledge) is BL-659.

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests` pin the HEADERS block for a plain GET, a POST with `-d`, and custom `-H` headers, and the output for a response with a body, with trailers, and with RST_STREAM mid-body, through a fake connection.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
