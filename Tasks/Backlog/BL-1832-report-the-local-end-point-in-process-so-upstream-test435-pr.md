---
id: BL-1832
title: Report the local end point in process so upstream test435 prints a real local port (GF-0002)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1832 — Report the local end point in process so upstream test435 prints a real local port (GF-0002)

## Goal

Upstream test435 passes in process: `%{local_port}` prints a number, as curl 8.21.0 does, so gap item `behaviour:test435` of GF-0002 measures `match`.

## Context

BL-1795 gave `SwsHttpServerConnection` a `LocalEndPoint` (127.0.0.1, 49152 and up, one port per connection) and pooled in-process connections, yet test435 still prints `local port == -1`. The connection's `RemoteEndPoint` is `null`; find where the HTTP handler or `EndPointReportingProtocolHandler` drops the local end point then, and report it. Reproduce: `dotnet test Curl.Conformance.UnitTests`, read test435's Inconclusive message.

## Acceptance criteria

- [ ] test435 passes in `UpstreamConformanceTests` and is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A unit test pins `%{local_port}` for a connection with a local but no remote end point.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-08: Created.
