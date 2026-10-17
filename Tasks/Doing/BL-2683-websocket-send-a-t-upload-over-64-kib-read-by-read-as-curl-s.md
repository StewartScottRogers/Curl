---
id: BL-2683
title: WebSocket: send a -T upload over 64 KiB read by read as curl's cr_ws_read does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2683 — WebSocket: send a -T upload over 64 KiB read by read as curl's cr_ws_read does

## Goal

`curl -T <file> ws://...` with a file larger than 64 KiB sends the bytes in the frames real curl 8.22.0 sends (one binary frame per upload read, as `lib/ws.c`'s `cr_ws_read`), not one frame for the whole file.

## Context

BL-2611 (ADR-0669) made `WsProtocolHandler` send a read callback's upload (`IWsUploadFrameSource`) read by read, 64 KiB at a time (`UploadReadSize`), but kept ADR-0131's one frame for every other upload, so the tool's `-T` is unchanged. For an upload under 64 KiB the bytes are the same; for a larger one curl sends several frames. Measure real curl first with `Record-CurlExchange.ps1` (`-T` of a 200 000-byte file to a loopback `ws://` server that answers `101`) to learn the frame sizes the tool's reads give, then send any upload through `WsUploadFrames` (or the same read loop) when that matches. Watch the `-v` `upload completely sent off` line, `%{size_upload}` and the `--trace-config ws` `WS-ENC` lines, which repeat per frame.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Ws.UnitTests` pins the frames a 200 000-byte upload stream that is not a read callback's is sent in, matching what real curl was measured to send (the measurement recorded under Notes).
- [ ] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 9a4e158c 53bb8082.
