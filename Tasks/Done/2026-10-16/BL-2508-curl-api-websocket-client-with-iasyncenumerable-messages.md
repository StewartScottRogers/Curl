---
id: BL-2508
title: Curl.Api: WebSocket client with IAsyncEnumerable messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2496, BL-2498]
touches: [Curl.Api/WebSockets, Curl.Api.UnitTests/WebSockets, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2508 — Curl.Api: WebSocket client with IAsyncEnumerable messages

## Goal

`CurlClient.ConnectWebSocketAsync` returns a `CurlWebSocket` with `SendAsync` (text or binary), `ReceiveAsync()`, an `IAsyncEnumerable<CurlWebSocketMessage>` of incoming messages, ping/pong and close with a status, all honouring `CancellationToken`.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to `IEasyWebSocket` (receive, send, metadata, start-frame, raw mode, no-autopong) over the connect-only connection. Fragmented messages are reassembled; the frame API is exposed for callers that want frames.

## Acceptance criteria

- [x] Tests with a fake `IEasyWebSocket` cover text, binary, fragmented messages, ping/pong, close by either side, cancellation of a pending receive, and a connection failure as `CurlException`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Touches: added `Curl.Api/CLAUDE.md` (acceptance criterion 5 keeps it true; no other task in Doing names it).
- Design (ADR-0633): `CurlClient.ConnectWebSocketAsync` (partial in `Curl.Api/WebSockets`) performs with `CURLOPT_CONNECT_ONLY` 2 and `CurlWebSocketOptions` (AutoPong, RawMode); `CurlWebSocket` gives whole messages (fragments joined, control frames between fragments kept apart), `ReadMessagesAsync` through the close frame, send methods for text, binary, ping, pong, close (status + reason) and raw, and the frame API (`ReceiveFrameAsync`, `SendFrameAsync`, `CurrentFrame`). `curl_ws_start_frame` is not exposed (read-callback only).
- Default taken: the engine answers `Again` instead of waiting, so a call waits 1 ms on `TimeProvider.System` and asks again; the wait is injected for tests.
- Coverage: async iterators and `await` inside `finally` leave compiler branches the collector counts, so `ReadMessagesAsync` returns a hand-written `CurlWebSocketMessageReader` and the connect path awaits the perform with `SuppressThrowing`.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` 100% line, 100% branch, 0 failing members (49 members, worst CRAP 8). Curl.Api.UnitTests 501 passed, 2 skipped (platform); full fast run green. `PassingUpstreamCases.txt` untouched.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. CurlClient.ConnectWebSocketAsync returns a CurlWebSocket with text/binary/fragmented messages, IAsyncEnumerable reading, ping/pong, close and cancellation
