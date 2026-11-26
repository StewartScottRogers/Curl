---
id: BL-6036
title: Bound the HTTP/2 closing GOAWAY when the peer has stopped reading
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitTests/Http2StalledGoAwayTests.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.StreamSessions.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6036 — Bound the HTTP/2 closing GOAWAY when the peer has stopped reading

## Goal

Closing an HTTP/2 connection ends within a bounded time when the peer has stopped reading and no earlier write was cancelled.

## Context

Found in BL-6006 (ADR-1421). `Http2Session.ShutDownAsync` is run with `CancellationToken.None` (`HttpProtocolHandler.ConnectionExchange.cs`, `PoolEntry`), so a GOAWAY the peer never takes waits for ever. A session has no `TimeProvider`.

## Acceptance criteria

- [x] A unit test whose fake connection never completes the GOAWAY write, with a fake `TimeProvider`, shows the close ending once a bounded time passes.
- [x] `dotnet build` is clean, and the fast tests pass with 100% line and branch coverage kept on the library.

## Notes

- `Http2Session.ShutDownAsync` now runs under a one-second limit on the session's `TimeProvider`, linked to the caller's token, covering the frame-lock wait, RST_STREAM, GOAWAY and flush (ADR-1433: same limit as ADR-1421; no change to `IConnectionSession`).
- Added to `touches` (named by no other task in Doing): `HttpProtocolHandler.ConnectionExchange.cs`, `HttpProtocolHandler.StreamSessions.cs` and `HttpH2cUpgradeConnection.cs`, so production sessions get the transfer's `TimeProvider`. The new constructor parameters are optional (null means the system clock), which leaves existing tests unchanged.
- Not measured against curl: curl writes its GOAWAY without blocking, so it has no output or exit code to match.
- Verified: build 0 warnings; format clean on the changed files; fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 0 failing members. Tests are in `Http2StalledGoAwayTests.cs`.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: b0c04a34 bd0dde9c.
- 2026-11-26: Doing -> Done. Closing an HTTP/2 connection gives up its closing frames after one second when the peer has stopped reading
