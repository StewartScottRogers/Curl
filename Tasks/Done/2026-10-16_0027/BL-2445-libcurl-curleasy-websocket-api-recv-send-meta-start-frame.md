---
id: BL-2445
title: Libcurl: CurlEasy WebSocket API (recv, send, meta, start_frame)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408, BL-2407]
touches: [Libcurl/WebSocket, Libcurl.UnitTests/WebSocket]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2445 — Libcurl: CurlEasy WebSocket API (recv, send, meta, start_frame)

## Goal

Libcurl `CurlEasy` has `WebSocketRecv`, `WebSocketSend`, `WebSocketMeta` and `WebSocketStartFrame` (`curl_ws_*`) over `IEasyWebSocket`, with the `CurlWebSocketFlags`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Fake-engine tests pin each call and its `CurlCode`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- The calls are `CurlEasy` extension methods in `Libcurl/WebSocket` (`Libcurl.WebSocket.CurlEasyWebSocket`), so `CurlEasy.cs` (sealed, not partial, outside `touches`) stays untouched: blocking `WebSocketRecv`, `WebSocketSend`, `WebSocketStartFrame` with libcurl's out parameters over `Async` forms, and `WebSocketMeta`. A `null` or cleaned-up handle answers `BadFunctionArgument` (`Meta` `null`) without the engine, as libcurl's `GOOD_EASY_HANDLE` check does. Decided in ADR-0585.
- `Libcurl/CLAUDE.md` is edited too, outside `touches`, because the acceptance criteria require it kept true.
- `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members. `Libcurl.UnitTests` 701 passed; every fast test project green; `PassingUpstreamCases.txt` unchanged.
- The managed engine still throws on `IEasyTransfer.WebSocket` until BL-2443/BL-2444; these calls are tested over fakes only.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CurlEasy has WebSocketRecv, WebSocketSend, WebSocketMeta and WebSocketStartFrame (curl_ws_*) over IEasyWebSocket, pinned by fake-engine tests
