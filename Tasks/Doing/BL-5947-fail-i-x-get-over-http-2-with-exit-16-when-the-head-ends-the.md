---
id: BL-5947
title: Fail -I -X GET over HTTP/2 with exit 16 when the head ends the stream short of content-length
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Http2IgnoredBodyLengthTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5947 — Fail -I -X GET over HTTP/2 with exit 16 when the head ends the stream short of content-length

## Goal

`curl -I -X GET` over HTTP/2 fails as curl does when the head ends the stream although it promises a body.

## Context

Measured in BL-5941 with `Record-CurlExchange.ps1 -ListenAddress <WSL gateway> -Curl wsl.exe` (curl 8.18.0, nghttp2), `-sS -v` `--http2-prior-knowledge -I -X GET` and `-Http2Frames 'H! :status: 200|content-length: 3'`: exit 16, `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`, `* closing connection #0`, `curl: (16) ...`. Curl exits 0: `Http2StreamConnection.EndResponseAsync` skips the length check when `EndsAtFinalHead`. A HEAD request (plain `-I`) with the same frames exits 0 and must stay so.

## Acceptance criteria

- [ ] A unit test shows exit 16 with that message for `-I -X GET` and exit 0 for plain `-I`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 2712ce70 3d836d5e.
