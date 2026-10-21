---
id: BL-1834
title: Report the local end point in process so upstream test435 prints a real local port (GF-0002)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1834 — Report the local end point in process so upstream test435 prints a real local port (GF-0002)

## Goal

Upstream test435 passes in process: `%{local_port}` prints a number, as curl 8.21.0 does, so gap item `behaviour:test435` of GF-0002 measures `match`.

## Context

BL-1795 gave `SwsHttpServerConnection` a `LocalEndPoint` (127.0.0.1, 49152 and up, one port per connection) and pooled in-process connections, yet test435 still prints `local port == -1`. The connection's `RemoteEndPoint` is `null`; find where the HTTP handler or `EndPointReportingProtocolHandler` drops the local end point then, and report it. Reproduce: `dotnet test Curl.Conformance.UnitTests`, read test435's Inconclusive message.

## Acceptance criteria

- [x] test435 passes in `UpstreamConformanceTests` and is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A unit test pins `%{local_port}` for a connection with a local but no remote end point.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Cause: the HTTP handler took `%{local_port}` only from `ConnectResult.LocalEndPoint`, which the in-process sws connector leaves empty; `ConnectionEndPointRecorder` records nothing for a connection without a remote end point, so the fallback in `EndPointReportingProtocolHandler` never applied. Fix: the HTTP report falls back to the connection's own `LocalEndPoint`.
- test435 then failed on `remote_ip`; the sws connection now reports 127.0.0.1 at the target's port as its remote end point (every host reaches the one loopback server, and `%HOSTIP` is 127.0.0.1). Added `Curl.Conformance.UnitLibrary` and `Curl.Conformance.UnitTests` to touches for this and `PassingUpstreamCases.txt`; no task in Doing on origin/work/dark-factory names them.
- `Curl.Console` needed no change. No Measure-CodeQuality run: the one new branch (`??`) is covered both ways by `ExecuteAsync_Exchange_ReportsWhatTheResponseSaid` and the new `ExecuteAsync_ConnectWithoutLocalEndPoint_ReportsTheConnectionsOwn`.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. test435 passes in process: HTTP reports the connection's own local end point and sws reports 127.0.0.1 at the target port
