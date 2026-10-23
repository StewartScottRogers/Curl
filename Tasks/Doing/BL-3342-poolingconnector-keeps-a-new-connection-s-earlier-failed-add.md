---
id: BL-3342
title: PoolingConnector keeps a new connection's earlier failed address socket error for CURLINFO_OS_ERRNO
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/PoolingConnector.cs, Curl.Networking.UnitTests/PoolingConnectorTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3342 — PoolingConnector keeps a new connection's earlier failed address socket error for CURLINFO_OS_ERRNO

## Goal

A transfer through the connection pool reports CURLINFO_OS_ERRNO 10061 on Windows (the platform's ECONNREFUSED elsewhere) when `localhost`'s `::1` is refused and `127.0.0.1` then connects, as libcurl 8.22.0 does.

## Context

BL-3328 made `TcpConnector` put the earlier failed address's socket error on `ConnectResult.LastSocketError` of a successful connect, and `EndPointRecordingConnector` records it. But `PoolingConnector` (Curl.Networking.UnitLibrary/PoolingConnector.cs, the `ConnectResult.Connected(new PooledConnection(...), ...)` built for a new connection, around line 616) rebuilds the result without `lastSocketError: connect.LastSocketError`, so Libcurl's easy handle, which connects through the pool, still reports 0. Found by BL-3315's differential run (LibcurlEasyDifferentialTests): get-follow/rother, userpwd-unrestricted and userpwd-redirect-other differ as `200019=0:10061` vs `200019=0:0`. A reused connection (`Reuse`) carries no earlier error: leave it at Success.

## Acceptance criteria

- [ ] `PoolingConnector.ConnectAsync` returns a new connection's `ConnectResult` with the wrapped connector's `LastSocketError`, pinned by a unit test in Curl.Networking.UnitTests; a reused connection's result has `SocketError.Success`.
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for Curl.Networking.UnitLibrary.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: ac4ea91c de10e31e.
