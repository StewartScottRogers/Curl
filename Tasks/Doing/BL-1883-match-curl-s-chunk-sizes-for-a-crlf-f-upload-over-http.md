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
completed:
---
# BL-1883 — Match curl's chunk sizes for a --crlf -F upload over HTTP

## Goal

`curl --crlf -F f=@f.txt URL` sends the same request bytes as curl 8.21.0 (414 bytes for f.txt = 'file body
').

## Context

BL-1875 (AF-0125) made `--crlf` convert HTTP bodies and send them chunked via `HttpCrlfUploadStream`. `-T` and `--data-binary @file` now match curl byte for byte, but `-F` sends 425 bytes where curl sends 414: the multipart body goes out in more chunks than curl's (likely one chunk per part read rather than curl's one chunk). Compare `request.bin` from `Record-CurlExchange.ps1` for both binaries and match curl's chunk boundaries.

## Acceptance criteria

- [ ] `Record-CurlExchange.ps1` with `--crlf -F f=@f.txt` gives identical request.bin for curl and Curl.
- [ ] `dotnet build` and the fast tests pass.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
