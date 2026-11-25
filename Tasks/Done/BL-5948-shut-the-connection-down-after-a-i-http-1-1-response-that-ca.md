---
id: BL-5948
title: Shut the connection down after a -I HTTP/1.1 response that carries body bytes
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionReuse.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionEnd.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Original.Protocol.Http.UnitTests/HeadResponseWithBodyBytesTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5948 — Shut the connection down after a -I HTTP/1.1 response that carries body bytes

## Goal

`curl -v -I` over HTTP/1.1, answered with a head and body bytes, ends the connection as curl does.

## Context

Measured in BL-5941, `Record-CurlExchange.ps1 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nno\n'`, `-sS -v -I`: the reference Windows curl and WSL curl 8.18.0 write `{ [3 bytes data]` then `* shutting down connection #0`. Curl writes the data line then `* Connection #0 to host ... left intact`. Measure a second URL on the same host and the same response without `Content-Length` before changing the reuse decision.

## Acceptance criteria

- [x] A unit test shows the `shutting down connection #0` line and that the connection is not reused.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-11-25 with `Record-CurlExchange.ps1`, reference Windows curl, `-sS -v -I`, two URLs on one host, the server holding the connection open: body bytes with `Content-Length: 3`, without `Content-Length`, on a `204` and on `HTTP/1.0` with `Connection: keep-alive` all end `{ [3 bytes data]`, `* shutting down connection #N`, and the second URL opens connection #1. The same head with no body bytes is left intact and reused. A `--digest` 401 with body bytes shuts down and retries on a new connection (Curl matched this already).
- Decision (matches curl, so no ADR): over HTTP/1.x, `-I` with any body bytes read along with the head makes the connection not reusable (`ReceivedUnwantedBody` in `HttpProtocolHandler.ConnectionReuse.cs`). HTTP/2 and HTTP/3 are unchanged: the stream, not the connection, ends. Bytes that arrive only in a later read are never read, as in curl.
- Compared the built Curl with real curl afterwards: the `-v` connection lines match. An unsolicited `101` under `-I` differs (curl exits 52): filed as BL-5954.
- `dotnet build` clean, fast tests pass, `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 626d8599 3d836d5e.
- 2026-11-25: Doing -> Done. curl -v -I over HTTP/1.x now shuts the connection down, and opens a new one, when body bytes arrive with the head
