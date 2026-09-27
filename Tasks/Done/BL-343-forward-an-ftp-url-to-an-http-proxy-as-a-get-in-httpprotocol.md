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
completed: 2026-09-27
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

- [x] A test pins the measured request bytes for `ftp://example.com/f.txt` through an HTTP proxy.
- [x] A test shows the response body `hello` reaches the output and the result is exit 0.
- [x] `http` and `https` behaviour is unchanged (existing tests green); 100% line and branch coverage of the changed code.

## Notes

- Delivered directly rather than through the full /feature agent chain: the change is one
  line of behaviour in one library, measured bytes already pinned by BL-330.
- `ForwardProxyOf` already forwarded any non-TLS URL through an HTTP-kind proxy, so an `ftp`
  URL reached the proxy in absolute form unchanged. The one difference from `http` was the
  `Host` line: curl 8.21.0 sends `Host: example.com:21`, keeping FTP's default port, while the
  absolute-form target drops it. New `HttpUrlText.HostHeaderAuthority` gives the host with its
  port always for any scheme other than `http`/`https` (libcurl omits the port only for
  http:80 and https:443); `HttpRequestHeadFormatter` uses it for `Host`. Target and redirect
  origin still use `HostAndPort`.
- Decision: `ftp` is not added to `SupportedSchemes`. The handler serves an `ftp` URL only when
  `Curl.Console` routes one here for a forwarded proxy (BL-344); claiming the scheme would
  route every `ftp` URL to HTTP. Documented on the class remarks. No ADR: ADR-0056 rule 3 covers it.
- Tests: `ExecuteAsync_FtpThroughProxy_ForwardsAGetAndWritesTheProxysBody` pins the measured
  request bytes and the `hello` body, exit 0, at 1-byte and whole reads;
  `HostHeaderAuthority_GivesTheHostAndPortTheHostLineCarries` covers both branches.
  Curl.Protocol.Http.UnitTests 740 passed. Coverage: HttpUrlText and HttpProtocolHandler 100%
  line and branch; HttpRequestHeadFormatter 100% line, and its one partial branch (line 102,
  the `--proxy-header` collection expression) is untouched code, not part of this change.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. HttpProtocolHandler forwards an ftp:// URL to an HTTP proxy as curl 8.21.0's GET, Host with :21, and writes the proxy's body
