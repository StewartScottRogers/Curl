---
id: BL-6035
title: Send --doh-url's A and AAAA queries on one HTTP/2 connection, as curl multiplexes them
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.Http2.cs, Curl.Networking.UnitLibrary/DohDnsResolver.Exchange.cs, Curl.Networking.UnitTests/DohDnsResolverHttp2Tests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6035 — Send --doh-url's A and AAAA queries on one HTTP/2 connection, as curl multiplexes them

## Goal

Under `--doh-url` over HTTP/2, the A and AAAA queries share one connection as two streams, with the connection count and `-v` lines curl prints.

## Context

ADR-1418 (BL-6015) gave `--doh-url` HTTP/2 with a connection for each query. curl sets `CURLOPT_PIPEWAIT` on its DoH sub-transfers (`lib/doh.c`) and can multiplex them. Measure curl 8.18.0 on Ubuntu against an HTTP/2 DoH server (Unbound, `--trace-config doh`) first.

## Acceptance criteria

- [ ] A unit test in `DohDnsResolverHttp2Tests` shows the connection count and streams curl was measured to use for the A and AAAA queries, and pins the `-v` lines measured.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
