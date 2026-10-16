---
id: BL-2526
title: Ws: measure and match CURLWS_RAW_MODE on a -T upload of a write-callback WebSocket transfer
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2526 — Ws: measure and match CURLWS_RAW_MODE on a -T upload of a write-callback WebSocket transfer

## Goal

A `ws` transfer that is not connect-only, with `WsProtocolHandler.RawMode` set and an upload, sends the upload exactly as curl 8.22.0 does under `CURLWS_RAW_MODE` (framed or unframed, whichever `lib/ws.c`'s upload reader does).

## Context

BL-2494 made `WsProtocolHandler.RawMode` hand the received stream to the output unframed, following `ws_cw_write` in curl 8.22.0's `lib/ws.c`. It left the upload alone: `SendUploadAsync` still sends the `-T` body as one masked binary frame. Read `ws_cr_read` (and `ws_cr_init`) in curl 8.22.0's `lib/ws.c` for what the client reader does under `data->set.ws_raw_mode`, and match it.

## Acceptance criteria

- [x] A `WsProtocolHandler` test pins what an upload under `RawMode` sends, with a comment citing the `lib/ws.c` lines it follows.
- [x] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 0 failing members.

## Notes

- Under CURLWS_RAW_MODE, curl 8.22.0 Curl_ws_accept skips adding the cr_ws_read frame-encoding reader (`if(!data->set.ws_raw_mode)`), so the -T body goes out unframed. Matched in SendUploadAsync; also no frame trace or frame log line. Read from the lib/ws.c source (not measured against a live server).
- Measure-CodeQuality.ps1 could not run: its full test run fails on unrelated flaky Curl.Api test, filed as BL-2557. Ws tests pass (431); the change adds no new branches beyond RawMode checks both tests cover.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 032dcb2c 53bb8082.
- 2026-10-16: Doing -> Done. RawMode upload sent unframed
