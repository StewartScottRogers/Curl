---
id: BL-5947
title: Fail -I -X GET over HTTP/2 with exit 16 when the head ends the stream short of content-length
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Http2IgnoredBodyLengthTests.cs, Curl.Original.Protocol.Http.UnitTests/Cgr138Tests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5947 — Fail -I -X GET over HTTP/2 with exit 16 when the head ends the stream short of content-length

## Goal

`curl -I -X GET` over HTTP/2 fails as curl does when the head ends the stream although it promises a body.

## Context

Measured in BL-5941 with `Record-CurlExchange.ps1 -ListenAddress <WSL gateway> -Curl wsl.exe` (curl 8.18.0, nghttp2), `-sS -v` `--http2-prior-knowledge -I -X GET` and `-Http2Frames 'H! :status: 200|content-length: 3'`: exit 16, `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`, `* closing connection #0`, `curl: (16) ...`. Curl exits 0: `Http2StreamConnection.EndResponseAsync` skips the length check when `EndsAtFinalHead`. A HEAD request (plain `-I`) with the same frames exits 0 and must stay so.

## Acceptance criteria

- [x] A unit test shows exit 16 with that message for `-I -X GET` and exit 0 for plain `-I`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-11-25, curl 8.18.0 (WSL, nghttp2 1.68.0), `--http2-prior-knowledge -v`, `-Http2Frames 'H! :status: 200|content-length: 3'`: `-I -X GET` and `-i` alike exit 16 and write the status line and header without the empty line (32 bytes of stdout); `-v` ends `< content-length: 3`, `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`, `* closing connection #0`. `'H! :status: 103|link: x'` shows the same line.
- So the head is refused where the 1xx head ending the stream already is (`MalformedHeadOf`), for GET with and without `-I`. Plain GET used to write the empty line; the `clshort, no DATA` row of `Cgr138Tests` and ADR-0847 now say what curl does. Both refused heads now report the message as a `-v` line.
- `touches` widened with `Cgr138Tests.cs` (that row pinned the empty line); no task in Doing names it. ADR-0847 amended rather than a new ADR: same decision, one more case.
- `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members. Fast tests green.
- Filed BL-5957 (`-I -X GET` ended short by a trailer or DATA, measured here: exit 0 but a shutdown Curl does not make) and BL-5958 (the `-v` line on the other shutdown paths).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 2712ce70 3d836d5e.
- 2026-11-25: Doing -> Done. A final HTTP/2 head ending the stream short of content-length now exits 16 for -I -X GET and GET, written as curl writes it; plain -I still exits 0.
