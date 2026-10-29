---
id: BL-4201
title: Add an HTTP/3 client session core to Curl.Http3.UnitLibrary that opens request streams with any pseudo-headers and reports the peer's SETTINGS
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Http3.UnitLibrary/Http3ClientSession.cs, Curl.Http3.UnitLibrary/Http3ClientRequestStream.cs, Curl.Http3.UnitLibrary/Curl.Http3.UnitLibrary.csproj, Curl.Http3.UnitTests/Http3ClientSessionTests.cs, Curl.Http3.UnitTests/Http3ClientRequestStreamTests.cs, Curl.Http3.UnitLibrary/Http3MultiplexedStreamAdapter.cs, Curl.Http3.UnitTests/Http3MultiplexedStreamAdapterTests.cs, Curl.Http3.UnitTests/InMemoryMultiplexedConnection.cs, Curl.Http3.UnitTests/InMemoryMultiplexedStream.cs, Curl.Http3.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4201 — Add an HTTP/3 client session core to Curl.Http3.UnitLibrary that opens request streams with any pseudo-headers and reports the peer's SETTINGS

## Goal

`Curl.Http3.UnitLibrary` holds a public HTTP/3 client session over `IMultiplexedConnection` (local control and QPACK streams, reading the peer's unidirectional streams, opening request streams) whose request stream sends HEADERS built from any field list the caller gives, pseudo-headers included (such as `:protocol` for RFC 9220 extended CONNECT), and which reports whether the peer's SETTINGS carried `SETTINGS_ENABLE_CONNECT_PROTOCOL` (0x08), with no curl-specific text, exit codes or logs in it.

## Context

Split from BL-4194 by its first pass. `Curl.Protocol.Http.UnitLibrary/Http3Session.cs` and `Http3StreamConnection.cs` cannot simply move: they implement `IHttpStreamSession` / `IHttpStreamConnection` and use `HttpTransferException`, `HttpTransferMessages`, `HttpFrameLog`, `Http3StreamTrace`, `HttpStreamOpenedLines`, `HttpConnectionInfoLines`, `HttpRequestHeadFormatter`, `Http2RequestHeaders` and `Http2ResponseHead`, all in `Curl.Protocol.Http.UnitLibrary`. So the protocol mechanics are extracted here first, reporting failures as `Http3Exception` (with its `Http3ErrorCode`) or typed results the caller maps, and exposing hooks (events or callbacks) where `Http3Session` today logs frames and trace lines, so BL-4194 can rebuild `Http3Session` and `Http3StreamConnection` as thin adapters over it without changing any curl output. Read both files first and take their logic as the model (stream opening, `EndRequestStream` accounting, GOAWAY and refused-stream handling, the peer stream reader task, the QPACK encoder/decoder with zero table capacity). `Curl.Http3.UnitLibrary` does not yet reference `Curl.Protocol.Abstractions.UnitLibrary` (where `IMultiplexedConnection` and `IMultiplexedStream` live): add that reference. Do not change `Curl.Protocol.Http.UnitLibrary`; that is BL-4194.

## Acceptance criteria

- [x] `Http3ClientSession` and `Http3ClientRequestStream` (or names that say what they do) are public types in `Curl.Http3.UnitLibrary` with unit tests in `Curl.Http3.UnitTests` against an in-memory `IMultiplexedConnection`.
- [x] A caller can send a request stream's HEADERS with any pseudo-header list, read the response HEADERS and DATA, and read whether the peer's SETTINGS carried `SETTINGS_ENABLE_CONNECT_PROTOCOL` (0x08); unit tests.
- [x] The session exposes enough (connection-level frames received, stream opened/closed, GOAWAY, errors as `Http3ErrorCode`) for `Curl.Protocol.Http`'s `Http3Session` to keep its present logs, trace lines and exit codes when rebuilt on it.
- [x] `Curl.Http3.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added four files to `touches`: the session reads peer streams through a `Stream`, so the library needs its own `IMultiplexedStream` adapter (`Http3MultiplexedStreamAdapter`), and the tests need an in-memory connection and stream, one type per file. No task in Doing on `origin/work/dark-factory` touches `Curl.Http3.UnitLibrary` or `Curl.Http3.UnitTests`.
- Design: `Http3ClientSession` takes the local SETTINGS and an optional callback for every frame read from the peer control stream (so the curl adapter keeps its SETTINGS and GOAWAY log lines, buffering them until a transfer log exists as it does today); `OpenRequestStreamAsync` takes an optional callback run once before the first stream (the trace's `ConnectionReady` line). Failures stay typed: a peer stream failure is kept as `ConnectionError` (an `Http3Exception`, `ClosedCriticalStream` for a reset critical stream, or a `QpackException`) and the request stream rethrows it under a read; a refused bidirectional open lets the connection's `IOException` through; a stream reset lets `MultiplexedStreamResetException` through. The curl adapter (BL-4194) maps these to curl text and exit codes as `Http3Session` does now.
- Hooks for BL-4194: the control-frame callback (SETTINGS and GOAWAY log lines), `beforeFirstStream` (trace `ConnectionReady`), `GoawayStreamId`, `AcceptsNewStreams`, `StopNewStreams`, `EndRequestStream` (streams left and in use), `ConnectionError` (code via `Http3Exception.ErrorCode` or `QpackException.ErrorCode`), `SendHeadersAsync` returning the section length (HEADERS sent log), and `ReadFrameOrDataAsync` returning each frame or DATA count (frame received and trace lines). The `IOException` mapping of a refused bidirectional open and the curl names (`ERR_H3_...`, `ERR_QPACK_...`) stay in the adapter.
- Also updated `Curl.Http3.UnitLibrary/CLAUDE.md` (its Abstractions rule and the new types), which no other task holds.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` reports 0 failing members; the Http3 tests (353) and the full fast run (51 test assemblies) pass, `dotnet build` has 0 warnings. The tests use in-memory streams only, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 51dea306 ca563086 efbb1cff bea20647 3d836d5e.
- 2026-10-29: Doing -> Done. Http3ClientSession and Http3ClientRequestStream in Curl.Http3.UnitLibrary open request streams with any pseudo-headers over IMultiplexedConnection and report the peer's SETTINGS_ENABLE_CONNECT_PROTOCOL
