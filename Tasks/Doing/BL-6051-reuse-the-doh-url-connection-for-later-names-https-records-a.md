---
id: BL-6051
title: Reuse the --doh-url connection for later names, HTTPS records and an HTTP/1.1 second query, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6038]
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/DohDnsResolver.Exchange.cs, Curl.Networking.UnitLibrary/DohDnsResolver.Http2.cs, Curl.Networking.UnitLibrary/DohDnsResolver.SubTransferTrace.cs, Curl.Networking.UnitLibrary/DohDnsResolver.HttpsRecord.cs, Curl.Networking.UnitTests/DohDnsResolverConnectionReuseTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6051 — Reuse the --doh-url connection for later names, HTTPS records and an HTTP/1.1 second query, as curl does

## Goal

Under `--doh-url`, a later lookup and an HTTP/1.1 server's second query go on the DoH connection already open, with the connection numbers and `-v` lines curl prints.

## Context

ADR-1435 (BL-6038) records what curl 8.18.0 on Ubuntu does: a second name in the same run goes on streams 5 and 7 of the first name's HTTP/2 connection, and against an HTTP/1.1 server (nginx) the second query waits and then reuses the first connection (`Reusing existing https: connection with host ...`, both `Connection #1`). `DohDnsResolver` closes each connection once its queries are answered. Measure the HTTPS-record query and the Windows reference before pinning.

## Acceptance criteria

- [ ] A unit test in `DohDnsResolverConnectionReuseTests` resolves two names over HTTP/2 and shows the connection count, stream numbers and `-v` lines curl was measured to use.
- [ ] A unit test there shows the same for both queries against an HTTP/1.1 DoH server.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 79961f58 94cff58a 3d836d5e.
