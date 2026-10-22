---
id: BL-2481
title: Engine: IEasyWebSocket partial frames (CURLWS_OFFSET) and curl_ws_start_frame
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2443]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2481 — Engine: IEasyWebSocket partial frames (CURLWS_OFFSET) and curl_ws_start_frame

## Goal

The managed engine's `ManagedEasyWebSocket.SendAsync` sends a frame in parts under `CURLWS_OFFSET` (the first call's `fragsize` the whole payload size, later calls continuing it), and `StartFrameAsync` (`curl_ws_start_frame`) starts a frame whose payload a WebSocket upload's read callback supplies, both as curl 8.22.0's `lib/ws.c` does.

## Context

ADR-0616 (BL-2443) built `curl_ws_recv`, `curl_ws_send` and `curl_ws_meta` over a `CURLOPT_CONNECT_ONLY` 2 connection, and answers `CURLE_BAD_FUNCTION_ARGUMENT` for `CURLWS_OFFSET` and for `StartFrameAsync`. `Curl.Protocol.Ws`'s `WsFrameEncoder` writes only whole frames, so a public way to write a frame head with the total length and then mask payload pieces at their offset goes there first. Read `lib/ws.c` (`curl_ws_send` with `CURLWS_OFFSET`, `curl_ws_start_frame`) for the checks and codes: a part longer than what is left of the frame, a new frame before the last one is finished, a call outside a read callback. Also correct `Curl.Api`'s `IEasyWebSocket.CurrentFrame` doc only if a task touching `Curl.Api.UnitLibrary` is free to: the engine returns the last received frame as libcurl's `curl_ws_meta` does (ADR-0616), not `null` outside the callback.

## Acceptance criteria

- [x] `ManagedEasyWebSocketTests` send a 10-byte text frame in two `CURLWS_OFFSET` parts over a fake connection and the bytes written are one frame head announcing 10 bytes and the masked payload.
- [x] A part past the announced size, and a new frame while one is unfinished, answer the codes `lib/ws.c` gives, with tests.
- [x] `StartFrameAsync` behaves as `curl_ws_start_frame` does inside and outside a read callback, with tests.
- [x] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Protocol.Ws.UnitLibrary` reports 0 failing members.

## Notes

- 2026-10-17 (lane 1): Took lane 5's work from factory/BL-2481-lane-5-20261016-081911 (721927d47 for Curl.Protocol.Ws, 5ebe907f0 for the engine); its ADR-0648 collided with an existing ADR-0648, so it is ADR-0665 now. Ws 437 and Engine 1774 fast tests pass; Measure-CodeQuality.ps1 for both libraries reports 0 failing members. Lane 5's follow-up BL-2522 (WebSocket upload through the read callback) was dropped by its rebase and is not refiled here; the CurrentFrame doc fix in Curl.Api stays out of touches.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 38ec9a67 b8258fe2 0cf7a8c9 0f90c956.
- 2026-10-16: Doing -> Backlog. Lane 5 could not integrate: rebase failed. The work is on branch factory/BL-2481-lane-5-20261016-081911; start with git cherry-pick --no-commit factory/BL-2481-lane-5-20261016-081911 and fix it.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 38ec9a67 b8258fe2 0cf7a8c9 0f90c956.
- 2026-10-17: Doing -> Blocked. Stewart: dark factory its runs of the last day already cost 2.77 of its 3.53 US dollar cost cap, so it was not run again; split the task; see Z:\repos\Curl.logs\BL-2481-*.jsonl
- 2026-10-17: Blocked -> Backlog. Cost cap window passed: its runs of the last 24 hours now leave 1 US dollar or more of its cost cap, so the cost cap allows a fresh run
- 2026-10-17: Backlog -> Doing. Criteria at claim: 38ec9a67 b8258fe2 0cf7a8c9 0f90c956.
- 2026-10-17: Doing -> Done. CURLWS_OFFSET partial sends and curl_ws_start_frame work in the managed engine, tests and quality gate green
