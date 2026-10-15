---
id: BL-2315
title: Define IEasyWebSocket, ITransferHeaders and the connect-only send and receive contracts in Curl.Api.UnitLibrary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2312]
touches: [Curl.Api.UnitLibrary/EngineContracts/IEasyTransfer.cs, Curl.Api.UnitLibrary/EngineContracts/WebSocket, Curl.Api.UnitLibrary/EngineContracts/Headers, Curl.Api.UnitLibrary/EngineContracts/ConnectOnly, Curl.Api.UnitTests/EngineContracts/WebSocket, Curl.Api.UnitTests/EngineContracts/Headers, Curl.Api.UnitTests/EngineContracts/ConnectOnly, Curl.Api.UnitTests/EngineContracts/Multi/MultiValueTypeTests.cs]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2315 — Define IEasyWebSocket, ITransferHeaders and the connect-only send and receive contracts in Curl.Api.UnitLibrary

## Goal

`Curl.Api.UnitLibrary/EngineContracts` defines `IEasyWebSocket` (in `WebSocket/`), `ITransferHeaders` (in `Headers/`) and the connect-only send and receive contract (in `ConnectOnly/`), and `IEasyTransfer` gains the members that reach them.

## Context

ADR-0506; plan sections "WebSocket", "Header API" and the easy `_recv`/`_send`; needs BL-2312. Upstream: curl 8.22.0's `include/curl/websockets.h`, `include/curl/header.h`, `docs/libcurl/curl_easy_recv.md`, `curl_easy_send.md`. Contracts only.

- `IEasyWebSocket`: `ReceiveAsync(Memory<byte>)` returning bytes received and frame metadata (`curl_ws_recv` + `curl_ws_meta`: age, flags, offset, bytes left, length), `SendAsync(ReadOnlyMemory<byte>, fragment size, flags)` (`curl_ws_send`), `StartFrameAsync(flags, length)` (`curl_ws_start_frame`); `[Flags]` enum of `CURLWS_*` with upstream values.
- `ITransferHeaders`: `TryGet(name, index, origin, request)` (`curl_easy_header`) and `Next(origin, request, previous)` (`curl_easy_nextheader`), a header record (name, value, amount, index, origin), `[Flags]` origin enum `CURLH_*`, results as `CurlHeaderCode`.
- Connect-only: `ReceiveAsync(Memory<byte>)` and `SendAsync(ReadOnlyMemory<byte>)` returning `CurlCode` and the byte count (`CURLE_AGAIN` when nothing is ready), valid after a perform with `CURLOPT_CONNECT_ONLY`.
- `IEasyTransfer` gains `WebSocket`, `Headers` and the connect-only members (this task edits `IEasyTransfer.cs`, which is in `touches`).

## Acceptance criteria

- [x] The three contracts and their enums exist in the named sub-folders, each member documented with the libcurl function or constant it stands for.
- [x] `IEasyTransfer` exposes them.
- [x] Tests in `Curl.Api.UnitTests/EngineContracts/WebSocket`, `/Headers` and `/ConnectOnly` pin every upstream constant (`CURLWS_*`, `CURLH_*`) and the metadata record's defaults.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

- Shapes decided in ADR-0512: `IEasyTransfer` gains `WebSocket`, `Headers` and `Connection` (not
  `ConnectOnly`, which already names the option enum); async calls return code-and-count record
  structs; `curl_ws_meta` is the nullable `IEasyWebSocket.CurrentFrame`; both `[Flags]` enums add
  `None = 0`; `ITransferHeaders.LastRequest` is the `request` argument -1.
- Added `Curl.Api.UnitTests/EngineContracts/Multi/MultiValueTypeTests.cs` to `touches`: its
  `UnusedTransfer` fake implements `IEasyTransfer` and must gain the three new properties. No other
  task in Doing on `origin/work/dark-factory` names it.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Api.UnitLibrary reports 0 failing members;
  Curl.Api.UnitTests 351 passed; all 38 fast test assemblies passed.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. IEasyTransfer exposes IEasyWebSocket, ITransferHeaders and IConnectOnlyConnection with CURLWS_/CURLH_ constants pinned
