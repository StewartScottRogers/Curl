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
- Returned to Backlog unstarted: this run's budget could not cover two real-curl measurements, the
  change across three libraries and a Measure-CodeQuality run.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Unstarted: run budget too small for the two real-curl measurements (CONNECT proxy, HTTP/2 trailers), the change across Http, Networking and Abstractions, and Measure-CodeQuality; plan in Notes
