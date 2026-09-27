---
id: BL-344
title: Route ftp:// through an HTTP proxy without -p to HttpProtocolHandler in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-338, BL-343]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-344 — Route ftp:// through an HTTP proxy without -p to HttpProtocolHandler in Curl.Console

## Goal

`curl -x http://<proxy> ftp://…` without `-p` is handed to `HttpProtocolHandler` with the proxy as `ForwardProxy`, so it is forwarded as an HTTP GET as curl 8.21.0 does.

## Context

- ADR-0056, rule 3. Only an `Http` or `Http10` proxy without `-p` takes this route; with `-p` or a SOCKS proxy, `ftp` tunnels through the FTP handler (rule 2) once that handler exists.
- Handler selection lives in `Curl.Console` (`CurlComposition` and the transfer runner); `HttpProtocolHandler` accepts the URL once BL-343 is done.

## Acceptance criteria

- [x] A test shows `-x http://127.0.0.1:1 ftp://example.com/f.txt` is executed by the HTTP handler with `ForwardProxy` set.
- [x] A test shows `-p -x http://127.0.0.1:1 ftp://example.com/f.txt` is not routed to the HTTP handler.
- [x] A test shows `ftp://` without a proxy is not routed to the HTTP handler.

## Notes

- 2026-09-27 (lane 1): The three acceptance tests exercise handler selection in `Curl.Console`, so they belong in `Curl.Console.UnitTests`, which `touches` did not name. Added it. BL-295 (in Doing) also touches `Curl.Console.UnitTests`, so this task returns to Backlog until BL-295 leaves Doing.
- 2026-09-27 (lane 2): Routing lives in a new `ForwardedFtpProtocolHandler` in `Curl.Console`, registered for `ftp` beside the HTTP handler it wraps, rather than special-casing the scheme in `Curl.Core`'s `ProtocolDispatcher` (outside `touches`). It forwards only an `Http`/`Http10` `ForwardProxy` without `ProxyTunnel`; anything else keeps today's exit 1 `Protocol "ftp" not supported` until an FTP handler exists, which will replace that fallback. An HTTPS proxy is not forwarded, as the task's Context and ADR-0056 rule 3 name only HTTP proxies. Tests: `ForwardedFtpProtocolHandlerTests` (every branch) and two end-to-end cases in `CurlCompositionProxyTests` (the forwarded GET bytes BL-330 measured; `-p` and no proxy never connect).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Needs Curl.Console.UnitTests for its tests, which BL-295 (in Doing) also touches; retry once BL-295 is no longer in Doing.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ftp:// through an HTTP proxy without -p is forwarded by HttpProtocolHandler as an HTTP GET
