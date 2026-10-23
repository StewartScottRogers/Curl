---
id: BL-3312
title: Make curl_ws_meta answer NULL outside a write callback
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl/WebSocket/CurlEasyWebSocket.cs, Libcurl.UnitTests/WebSocket]
requirement: none
created: 2026-10-23
completed:
---
# BL-3312 — Make curl_ws_meta answer NULL outside a write callback

## Goal

`curl_ws_meta` (`CurlEasyWebSocket.WebSocketMeta`) answers NULL when called outside a write callback, as libcurl 8.22.0 does, instead of the frame the last `curl_ws_recv` returned.

## Context

Found by BL-3240's WebSocket differential run (case `ws_meta_outside_callback`, `Curl.Conformance.Differential.UnitLibrary/Libcurl/WebSocket/LibcurlWebSocketCases.cs`): on a `CURLOPT_CONNECT_ONLY` 2 handle, after a `curl_ws_recv` that returned a 1-byte text frame, libcurl 8.22.0 (Git for Windows ucrt64 libcurl-4.dll, Schannel) answers `curl_ws_meta` with NULL, while Curl's Libcurl answers `age=0 flags=1 offset=0 bytesleft=0 len=1`. libcurl's documentation says curl_ws_meta only works from within the write callback. Start at `Libcurl/WebSocket/CurlEasyWebSocket.cs` `WebSocketMeta` (it answers `LiveWebSocket(easy)?.CurrentFrame`). Inside a callback-mode write callback both libraries already agree (case `ws_callback_mode`).

## Acceptance criteria

- [ ] `WebSocketMeta` on a connect-only handle answers null outside a write callback, after a receive, and still answers the current frame inside a write callback.
- [ ] A focused unit test in Libcurl.UnitTests pins both (ADR-0482).
- [ ] With CURL_DIFFERENTIAL_REFERENCE_LIBRARY set, LibcurlWebSocketDifferentialTests reports ws_meta_outside_callback identical.
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Libcurl.

## Notes

## Log

- 2026-10-23: Created.
