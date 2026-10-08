---
id: BL-1609
title: Count CONNECT reply heads and HTTP/2 and HTTP/3 trailers toward curl's 5000 response header limit
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1448]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: FR-067
created: 2026-10-07
completed:
---
# BL-1609 — Count CONNECT reply heads and HTTP/2 and HTTP/3 trailers toward curl's 5000 response header limit

## Goal

A transfer through an HTTP proxy tunnel counts the CONNECT reply's headers, and an HTTP/2 or HTTP/3 response counts its trailers, toward curl 8.21.0's limit of 5000 stored response headers, failing the 5001st with exit 100 `Too many response headers, 5000 is max` as curl does.

## Context

- BL-1448 made the HTTP handler count a transfer's heads across `-L` hops and in-handler resends (`HttpRequestOptions.ResponseHeadersStored`, `TransferReport.ResponseHeadersStored`) and HTTP/1.1 chunked trailers (`HttpChunkedDecoder.TrailerLimit`). Two kinds of stored header are still not counted:
  - CONNECT reply heads: upstream `lib/http_proxy.c`/`cf-h1-proxy.c` push them with `CLIENTWRITE_CONNECT`, which `Curl_headers_push` (`lib/headers.c`, tag `curl-8_21_0`) stores. The tunnel is opened in the connector (`Curl.Networking.UnitLibrary`), whose `ConnectResult` would have to report the count to the handler.
  - HTTP/2 and HTTP/3 trailers: `HttpProtocolHandler.TrailersOfAsync` takes them from `IHttpStreamConnection.TrailerBytes` uncounted.
- Measure each with `Record-CurlExchange.ps1` before pinning the bytes (`-v`, `-i`, exit code), as BL-1448 Notes did.

## Acceptance criteria

- [ ] A test pins the measured exit code, header output and last `<` line for a CONNECT reply of 3000 headers followed by a final head of 3000 headers, or Notes record the measurement showing curl does not count CONNECT heads.
- [ ] A test pins the measured behaviour for HTTP/2 trailers that take the count past 5000, or Notes record the measurement showing curl does not count them.
- [ ] `Measure-CodeQuality.ps1 -Library` naming every library changed reports no failing member.

## Notes

- 2026-10-07 (lane 4): `ConnectResult` lives in `Curl.Protocol.Abstractions.UnitLibrary`, not in
  `Curl.Networking.UnitLibrary`, so reporting the CONNECT reply's header count to the handler needs
  that library and its tests; added both to `touches` (no task in Doing on `origin/work/dark-factory`
  names them). Plan: add a `ResponseHeadersStored`-style count to `ConnectResult`, seed the handler's
  first hop from it (`HttpProtocolHandler.cs` ~1132), and count `requestStream.TrailerBytes` lines in
  `TrailersOfAsync` (~1843) against the same 5000 limit. Both measurements need a loopback proxy and an
  HTTP/2 trailer server in `Record-CurlExchange.ps1`, which it may not have yet.
- Measured 2026-10-07 (lane 8), curl 8.21.0 (mingw64, Schannel), `Record-CurlExchange.ps1 -Script`
  (a plain proxy: `read`, send the CONNECT reply, `read`, send the final response, `pause 500`,
  `close`; the `-Response` mode answers one request per connection, so it cannot serve a tunnel),
  `-sv -i -p -x http://127.0.0.1:18609 http://example.test/`: CONNECT reply
  `HTTP/1.1 200 Connection established` with `X-C1..X-C3000`, then `200` with `Content-Length: 2`
  and `X-H1..X-H3000`: **exit 100**. The CONNECT heads count: 3000 + `Content-Length` +
  `X-H1..X-H1999` = 5000, `< X-H2000: v` is the last `<` line, then
  `* Too many response headers, 5000 is max` and `* closing connection #0`. The header output holds
  the whole CONNECT head (with its blank line) and the final head through `X-H1999: v\r\n`, no blank
  line, no body (57849 bytes). Status lines are not counted, as in BL-1448.
- HTTP/2 trailers cannot be measured on this machine: the Windows curl 8.21.0 has no HTTP2 feature
  (no nghttp2), and `Record-CurlExchange.ps1` serves no h2 origin (only an h2 proxy). By source,
  `lib/headers.c`'s client writer pushes every `CLIENTWRITE_HEADER` write, trailers
  (`CLIENTWRITE_TRAILER`) included, through `Curl_headers_push`, the same path whose limit BL-1448
  measured for chunked trailers; so the next run should count them too and record that in an ADR.
- Simpler seam found: the handler need not read a count from `ConnectResult`. The CONNECT head
  already reaches the transfer through `IConnectReplyHeadWritingEvents.WriteConnectReplyHeadAsync`
  (`Curl.Console/ConnectReplyHeadWritingEvents.cs`), so either that writer counts the head's lines,
  or `ConnectResult` gets a `ConnectReplyHeadersStored` set from the opening reply's head in
  `TcpConnector.SecureOpenedTunnelAsync` (also rebuilt in `PoolingConnector.cs` ~550).
- Returned to Backlog again (lane 8): measured CONNECT, but the run's budget could not cover the
  change across three libraries and the Measure-CodeQuality run.
- Returned to Backlog unstarted: this run's budget could not cover two real-curl measurements, the
  change across three libraries and a Measure-CodeQuality run.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Unstarted: run budget too small for the two real-curl measurements (CONNECT proxy, HTTP/2 trailers), the change across Http, Networking and Abstractions, and Measure-CodeQuality; plan in Notes
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. CONNECT measured (heads count, exit 100, last < X-H2000; Notes); left: count CONNECT heads and HTTP/2-3 trailers in Http/Networking/Abstractions, tests, Measure-CodeQuality
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Code and tests written, uncommitted (shift stash): ConnectResult.ConnectReplyHeadersStored, HttpProxyTunnelReply.HeaderCount, TcpConnector/PoolingConnector pass it, handler seeds HeadersStoredBefore and cuts HTTP/2-3 trailers (StoredLengthOf); 3 test projects green. Left: full build + fast tests, Measure-CodeQuality on 3 libraries, ADR for trailer counting, commit
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Unstarted: lane 9 found a clean worktree and no access to the earlier lane's shift stash holding the written code (ConnectResult.ConnectReplyHeadersStored etc.); the run's budget could not cover rewriting it across three libraries plus Measure-CodeQuality. Next run: apply that stash by hash first
- 2026-10-07: Backlog -> Doing.
