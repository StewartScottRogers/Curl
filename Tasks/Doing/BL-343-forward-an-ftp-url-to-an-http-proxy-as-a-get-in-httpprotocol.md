---
id: BL-343
title: Forward an ftp:// URL to an HTTP proxy as a GET in HttpProtocolHandler
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-343 — Forward an ftp:// URL to an HTTP proxy as a GET in HttpProtocolHandler

## Goal

`HttpProtocolHandler`, given an `ftp://` URL and an HTTP `ForwardProxy` without `ProxyTunnel`, sends curl 8.21.0's forwarded GET to the proxy and writes the proxy's response as for any HTTP transfer.

## Context

- ADR-0056, rule 3: libcurl hands `ftp` through an HTTP proxy to its HTTP code when not tunnelling.
- Measured by BL-330: `curl -sS -x http://127.0.0.1:18332 ftp://example.com/f.txt` sent `GET ftp://example.com/f.txt HTTP/1.1\r\nHost: example.com:21\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n`; the reply `HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello` gave stdout `hello`, exit 0. `Host` carries `:21`, FTP's default port.
- Routing from `Curl.Console` is BL-344.
- Where a criterion says *measured*, the bytes were measured by BL-330 with curl 8.21.0 (`/mingw64/bin/curl`, ADR-0009) against a loopback listener and are recorded under BL-330's `Notes`. Pin only those bytes; re-measure with `Record-CurlExchange.ps1` for anything else.

## Acceptance criteria

- [ ] A test pins the measured request bytes for `ftp://example.com/f.txt` through an HTTP proxy.
- [ ] A test shows the response body `hello` reaches the output and the result is exit 0.
- [ ] `http` and `https` behaviour is unchanged (existing tests green); 100% line and branch coverage of the changed code.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
