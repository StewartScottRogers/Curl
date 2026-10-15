---
id: BL-2315
title: Define IEasyWebSocket, ITransferHeaders and the connect-only send and receive contracts in Curl.Api.UnitLibrary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2312]
touches: [Curl.Api.UnitLibrary/EngineContracts/IEasyTransfer.cs, Curl.Api.UnitLibrary/EngineContracts/WebSocket, Curl.Api.UnitLibrary/EngineContracts/Headers, Curl.Api.UnitLibrary/EngineContracts/ConnectOnly, Curl.Api.UnitTests/EngineContracts/WebSocket, Curl.Api.UnitTests/EngineContracts/Headers, Curl.Api.UnitTests/EngineContracts/ConnectOnly]
requirement: none
created: 2026-10-15
completed:
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

- [ ] The three contracts and their enums exist in the named sub-folders, each member documented with the libcurl function or constant it stands for.
- [ ] `IEasyTransfer` exposes them.
- [ ] Tests in `Curl.Api.UnitTests/EngineContracts/WebSocket`, `/Headers` and `/ConnectOnly` pin every upstream constant (`CURLWS_*`, `CURLH_*`) and the metadata record's defaults.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
