---
id: BL-1193
title: Write the [HTTP-PROXY] and [H1-PROXY] trace lines of a CONNECT tunnel for --trace-config
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1193 — Write the [HTTP-PROXY] and [H1-PROXY] trace lines of a CONNECT tunnel for --trace-config

## Goal

Curl writes curl 8.21.0's `[HTTP-PROXY]` and `[H1-PROXY]` lines around a CONNECT tunnel (`-p`, or an https:// URL through `-x http://`) under their `--trace-config` names, `proxy`, `all` and `-vvvv`.

## Context

- Split from BL-1160 (ADR-0357's BL-1160 amendment), which delivered the [HAPROXY] lines and pinned [SETUP] through a plain HTTP proxy. The tunnel runs in `TcpConnector.ConnectThroughProxyAsync` and `HttpProxyTunnel`; `-vv` there should already be checked for its `[SETUP]` lines, which are untraced on that path today.
- Measure first with `Record-CurlExchange.ps1`; extend it to play the proxy if it cannot (it serves one HTTP exchange, so a plain `-x` proxy works already). Each line's place relative to `[SETUP]`, `[DNS]`, `Established connection` and the CONNECT lines matters; [HAPPY-EYEBALLS], [TCP], [MULTI] and [TIMER] are other tasks.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (a successful transfer and a refused connect), stderr in Notes; which `--trace-config` groups (`proxy`, `network`, `all`) turn the lines on is measured too.
- [x] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the stable lines and that no line appears without its component; ADR-0357 gets an amendment for any volatile value.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02 with `Record-CurlExchange.ps1 -Script` playing the proxy (read the CONNECT, send `HTTP/1.1 200 Connection established`, read the GET, answer it), curl 8.21.0 Schannel. `-s -v --trace-config proxy -p -x http://127.0.0.1:18932 http://example.test/x`, exit 0:

  ```
  *   Trying 127.0.0.1:18932...
  * [HTTP-PROXY] CONNECT
  * CONNECT: no ALPN negotiated
  * [HTTP-PROXY] installing subfilter for HTTP/1.1
  * [H1-PROXY] connect
  * [H1-PROXY] CONNECT start
  * Establishing HTTP proxy tunnel to example.test:80
  * [H1-PROXY] new tunnel state 'connect'
  * [H1-PROXY] CONNECT send
  > CONNECT example.test:80 HTTP/1.1 ... (head)
  * [H1-PROXY] new tunnel state 'receive'
  * [H1-PROXY] CONNECT receive
  * [HTTP-PROXY] CONNECT
  * [H1-PROXY] connect
  * [H1-PROXY] CONNECT receive
  < HTTP/1.1 200 Connection established
  <
  * [H1-PROXY] new tunnel state 'response'
  * [H1-PROXY] CONNECT response
  * [H1-PROXY] new tunnel state 'established'
  * CONNECT phase completed for HTTP proxy
  * CONNECT tunnel established, response 200
  * [H1-PROXY] new tunnel state 'failed'
  * Established connection to 127.0.0.1 (127.0.0.1 port 18932) from 127.0.0.1 port 60323
  * [HTTP-PROXY] removing connected setup filter
  * [HTTP-PROXY] destroy
  * [H1-PROXY] query ALPN
  * using HTTP/1.x
  ```
- Groups: `http-proxy` writes only the `[HTTP-PROXY]` lines, `h1-proxy` only the `[H1-PROXY]` ones, `proxy` and `--trace-config all` both. `network` writes neither (and no `[TCP] query ALPN` on this path, although it writes the GET's `[TCP] send/recv`); `-vvvv` writes neither (only `[SETUP]` lines), like `[SOCKS]` in BL-1191.
- `-vv` (setup): `[SETUP] added`, `[SETUP] happy eyeballing to proxy 127.0.0.1:P`, `Trying`, `[SETUP] added HTTP proxy tunnel filter`, `CONNECT: no ALPN negotiated`, ..., `Established connection`, `[SETUP] removing connected setup filter`, `[SETUP] destroy`. Under `all`: `[SETUP] added` before `[DNS] created`, and after `Established connection` the removals in the order `[DNS]`, `[SETUP]`, `[HTTP-PROXY]`, `[HAPPY-EYEBALLS]`, then `[H1-PROXY] query ALPN`.
- `https://example.test/x` through `-x http://` with `--trace-config proxy,setup` (the scripted proxy closes after the 200, exit 35): no `[SETUP] added`; the same tunnel lines; `[SETUP] added SSL filter for origin` right after `[H1-PROXY] new tunnel state 'failed'`, then the handshake's lines.
- Refused dial (`-x http://127.0.0.1:1`, `proxy,setup`, exit 7): `[SETUP] added`, `[SETUP] happy eyeballing to proxy 127.0.0.1:1`, `Trying`, the connect failure lines; no tunnel line.
- Proxy answering 403 (exit 7): the lines as above up to `CONNECT response`, then `[H1-PROXY] new tunnel state 'failed'` and `CONNECT tunnel failed, response 403`.
- Decisions (ADR-0357's BL-1193 amendment): one poll round fixed (volatile); `[SETUP]` on the tunnel path is written for `Http`/`Http10` proxies only; the HTTPS-proxy tunnel is filed as BL-1255; a second CONNECT after a `407` writes the same lines again (unmeasured). Wrapping the tunnel's events in the setup filter would have hidden the CONNECT reply head from the header output, so `TunnelRequest.HeadOutput` keeps the unwrapped events (this also fixes the same loss under `[DNS]` tracing).
- Tests: `TcpConnectorTests.HttpProxyTunnelTrace` (12) and `CurlCompositionHttpProxyTraceTests` (17 rows). `Measure-CodeQuality.ps1`: Curl.Networking.UnitLibrary and Curl.Console 100% line and branch, 0 failing members.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config http-proxy/h1-proxy/proxy/all write curl's [HTTP-PROXY] and [H1-PROXY] tunnel lines; -vv writes [SETUP] on the tunnel path; HTTPS-proxy tunnel filed as BL-1255
