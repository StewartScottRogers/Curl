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
completed:
---
# BL-3349 — Reuse the first https connection for a --parallel transfer to the same host when ALPN picks HTTP/1.1

## Goal

`-Z` transfers to one https host whose ALPN picks HTTP/1.1 print `%{num_connects}` as real curl does: the second reuses the first's connection (`1 0`).

## Context

Split from BL-3293. Measured with curl 8.21.0 (Schannel, no HTTP/2): `curl -sS --parallel -o NUL -o NUL -w '%{http_version} %{num_connects}\n' https://www.cloudflare.com/ https://www.cloudflare.com/cdn-cgi/trace` prints `1.1 1` then `1.1 0`; Curl prints `1.1 1` twice. `Curl.Console/ParallelHostQueue.cs` holds back the second transfer only for `http://` URLs (`DestinationOf`), so an https transfer opens its own connection once `PoolingConnector` (`WaitsForMultiplexing`, negotiation wait) learns ALPN chose HTTP/1.1. Real curl makes the waiter pending until the first transfer ends and then reuses its kept-alive connection, as ADR-0786 records for http. Reproduce on loopback with `Record-CurlExchange.ps1 -Tls` and `-Connections 2` and a keep-alive response, then fix.

## Acceptance criteria

- [ ] A `-Z` run of two https URLs to one HTTP/1.1 host with a kept-alive connection reports `%{num_connects}` 1 then 0, pinned by a unit test.
- [ ] An https host that negotiates h2 still multiplexes both transfers on one connection.
- [ ] The build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: e29e1a80 d89098e4 c46ca32a.
