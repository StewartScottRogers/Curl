---
id: BL-2508
title: Curl.Api: WebSocket client with IAsyncEnumerable messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2496, BL-2498]
touches: [Curl.Api/WebSockets, Curl.Api.UnitTests/WebSockets]
requirement: none
created: 2026-10-16
completed:
---
# BL-2508 — Curl.Api: WebSocket client with IAsyncEnumerable messages

## Goal

`CurlClient.ConnectWebSocketAsync` returns a `CurlWebSocket` with `SendAsync` (text or binary), `ReceiveAsync()`, an `IAsyncEnumerable<CurlWebSocketMessage>` of incoming messages, ping/pong and close with a status, all honouring `CancellationToken`.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to `IEasyWebSocket` (receive, send, metadata, start-frame, raw mode, no-autopong) over the connect-only connection. Fragmented messages are reassembled; the frame API is exposed for callers that want frames.

## Acceptance criteria

- [ ] Tests with a fake `IEasyWebSocket` cover text, binary, fragmented messages, ping/pong, close by either side, cancellation of a pending receive, and a connection failure as `CurlException`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
