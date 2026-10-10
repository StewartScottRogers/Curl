---
id: BL-1883
title: Match curl's chunk sizes for a --crlf -F upload over HTTP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1883 — Match curl's chunk sizes for a --crlf -F upload over HTTP

## Goal

`curl --crlf -F f=@f.txt URL` sends the same request bytes as curl 8.21.0 (414 bytes for f.txt = 'file body
').

## Context

BL-1875 (AF-0125) made `--crlf` convert HTTP bodies and send them chunked via `HttpCrlfUploadStream`. `-T` and `--data-binary @file` now match curl byte for byte, but `-F` sends 425 bytes where curl sends 414: the multipart body goes out in more chunks than curl's (likely one chunk per part read rather than curl's one chunk). Compare `request.bin` from `Record-CurlExchange.ps1` for both binaries and match curl's chunk boundaries.

## Acceptance criteria

- [x] `Record-CurlExchange.ps1` with `--crlf -F f=@f.txt` gives identical request.bin for curl and Curl.
- [x] `dotnet build` and the fast tests pass.

## Notes

- Measured curl 8.21.0 (`-s --crlf -F "f=@<abs>\f.txt;filename=f.txt"`): 414 bytes, the whole
  multipart body in one `cb` chunk. Curl sent 425 bytes in three chunks (`8a`, `b`, `36`):
  the multipart body gives one part per read and the chunked writer sends one chunk per read.
- Fix: `HttpCrlfUploadStream.Read`/`ReadAsync` now fill the caller's buffer until the source
  ends, as curl fills its upload buffer before sending a chunk. After it, request.bin matches
  curl byte for byte with boundary and port normalised (414 bytes, one `cb` chunk).
- Record-CurlExchange.ps1 runs curl from another directory, so the `-F` file needs an
  absolute path (a relative one gives exit 26).

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. curl --crlf -F sends the multipart body in one chunk, request.bin matches curl 8.21.0
