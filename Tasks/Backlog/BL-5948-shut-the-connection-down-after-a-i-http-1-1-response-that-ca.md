---
id: BL-5948
title: Shut the connection down after a -I HTTP/1.1 response that carries body bytes
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Original.Protocol.Http.UnitTests/HeadResponseWithBodyBytesTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5948 — Shut the connection down after a -I HTTP/1.1 response that carries body bytes

## Goal

`curl -v -I` over HTTP/1.1, answered with a head and body bytes, ends the connection as curl does.

## Context

Measured in BL-5941, `Record-CurlExchange.ps1 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nno\n'`, `-sS -v -I`: the reference Windows curl and WSL curl 8.18.0 write `{ [3 bytes data]` then `* shutting down connection #0`. Curl writes the data line then `* Connection #0 to host ... left intact`. Measure a second URL on the same host and the same response without `Content-Length` before changing the reuse decision.

## Acceptance criteria

- [ ] A unit test shows the `shutting down connection #0` line and that the connection is not reused.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
