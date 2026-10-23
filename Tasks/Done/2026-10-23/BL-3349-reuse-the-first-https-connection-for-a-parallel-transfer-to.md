---
id: BL-3349
title: Reuse the first https connection for a --parallel transfer to the same host when ALPN picks HTTP/1.1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/ParallelHostQueue.cs, Curl.Networking.UnitLibrary/PoolingConnector.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3349 — Reuse the first https connection for a --parallel transfer to the same host when ALPN picks HTTP/1.1

## Goal

`-Z` transfers to one https host whose ALPN picks HTTP/1.1 print `%{num_connects}` as real curl does: the second reuses the first's connection (`1 0`).

## Context

Split from BL-3293. Measured with curl 8.21.0 (Schannel, no HTTP/2): `curl -sS --parallel -o NUL -o NUL -w '%{http_version} %{num_connects}\n' https://www.cloudflare.com/ https://www.cloudflare.com/cdn-cgi/trace` prints `1.1 1` then `1.1 0`; Curl prints `1.1 1` twice. `Curl.Console/ParallelHostQueue.cs` holds back the second transfer only for `http://` URLs (`DestinationOf`), so an https transfer opens its own connection once `PoolingConnector` (`WaitsForMultiplexing`, negotiation wait) learns ALPN chose HTTP/1.1. Real curl makes the waiter pending until the first transfer ends and then reuses its kept-alive connection, as ADR-0786 records for http. Reproduce on loopback with `Record-CurlExchange.ps1 -Tls` and `-Connections 2` and a keep-alive response, then fix.

## Acceptance criteria

- [x] A `-Z` run of two https URLs to one HTTP/1.1 host with a kept-alive connection reports `%{num_connects}` 1 then 0, pinned by a unit test.
- [x] An https host that negotiates h2 still multiplexes both transfers on one connection.
- [x] The build is clean and the fast tests pass.

## Notes

- Fix is in `PoolingConnector.MayMultiplex`: a direct TLS connection (no proxy) now keeps its negotiation open until a session is held or its last lease ends, whatever ALPN picked, so a `-Z` waiter for the same https host waits for the returned HTTP/1.1 connection and reuses it (`1 0`); h2 still decides at `SessionHeld` and multiplexes. `ParallelHostQueue` needed no change. Pinned by `ConnectAsync_WaitingForATlsConnectionThatAgreedHttp11_ReusesItOnceItIsReturned` and the existing h2 test. Test is platform-neutral.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: e29e1a80 d89098e4 c46ca32a.
- 2026-10-23: Doing -> Done. A -Z run of https transfers to one HTTP/1.1 host reuses the first connection; h2 still multiplexes
