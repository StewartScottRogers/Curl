---
id: BL-6036
title: Bound the HTTP/2 closing GOAWAY when the peer has stopped reading
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitTests/Http2StalledGoAwayTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6036 — Bound the HTTP/2 closing GOAWAY when the peer has stopped reading

## Goal

Closing an HTTP/2 connection ends within a bounded time when the peer has stopped reading and no earlier write was cancelled.

## Context

Found in BL-6006 (ADR-1421). `Http2Session.ShutDownAsync` is run with `CancellationToken.None` (`HttpProtocolHandler.ConnectionExchange.cs`, `PoolEntry`), so a GOAWAY the peer never takes waits for ever. A session has no `TimeProvider`.

## Acceptance criteria

- [ ] A unit test whose fake connection never completes the GOAWAY write, with a fake `TimeProvider`, shows the close ending once a bounded time passes.
- [ ] `dotnet build` is clean, and the fast tests pass with 100% line and branch coverage kept on the library.

## Notes

## Log

- 2026-11-26: Created.
