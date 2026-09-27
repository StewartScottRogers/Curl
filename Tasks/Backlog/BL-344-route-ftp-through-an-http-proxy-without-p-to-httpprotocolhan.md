---
id: BL-344
title: Route ftp:// through an HTTP proxy without -p to HttpProtocolHandler in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-338, BL-343]
touches: [Curl.Console]
requirement: none
created: 2026-09-27
completed:
---
# BL-344 — Route ftp:// through an HTTP proxy without -p to HttpProtocolHandler in Curl.Console

## Goal

`curl -x http://<proxy> ftp://…` without `-p` is handed to `HttpProtocolHandler` with the proxy as `ForwardProxy`, so it is forwarded as an HTTP GET as curl 8.21.0 does.

## Context

- ADR-0056, rule 3. Only an `Http` or `Http10` proxy without `-p` takes this route; with `-p` or a SOCKS proxy, `ftp` tunnels through the FTP handler (rule 2) once that handler exists.
- Handler selection lives in `Curl.Console` (`CurlComposition` and the transfer runner); `HttpProtocolHandler` accepts the URL once BL-343 is done.

## Acceptance criteria

- [ ] A test shows `-x http://127.0.0.1:1 ftp://example.com/f.txt` is executed by the HTTP handler with `ForwardProxy` set.
- [ ] A test shows `-p -x http://127.0.0.1:1 ftp://example.com/f.txt` is not routed to the HTTP handler.
- [ ] A test shows `ftp://` without a proxy is not routed to the HTTP handler.

## Notes

## Log

- 2026-09-27: Created.
