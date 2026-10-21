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
completed: 2026-10-17
---
# BL-2683 — WebSocket: send a -T upload over 64 KiB read by read as curl's cr_ws_read does

## Goal

`curl -T <file> ws://...` with a file larger than 64 KiB sends the bytes in the frames real curl 8.22.0 sends (one binary frame per upload read, as `lib/ws.c`'s `cr_ws_read`), not one frame for the whole file.

## Context

BL-2611 (ADR-0669) made `WsProtocolHandler` send a read callback's upload (`IWsUploadFrameSource`) read by read, 64 KiB at a time (`UploadReadSize`), but kept ADR-0131's one frame for every other upload, so the tool's `-T` is unchanged. For an upload under 64 KiB the bytes are the same; for a larger one curl sends several frames. Measure real curl first with `Record-CurlExchange.ps1` (`-T` of a 200 000-byte file to a loopback `ws://` server that answers `101`) to learn the frame sizes the tool's reads give, then send any upload through `WsUploadFrames` (or the same read loop) when that matches. Watch the `-v` `upload completely sent off` line, `%{size_upload}` and the `--trace-config ws` `WS-ENC` lines, which repeat per frame.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Ws.UnitTests` pins the frames a 200 000-byte upload stream that is not a read callback's is sent in, matching what real curl was measured to send (the measurement recorded under Notes).
- [x] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 0 failing members.

## Notes

- Measured (reference curl 8.22.0, `Record-CurlExchange.ps1 -Port 18683`, response `101`, `-sS -v --trace-config ws -T <200 000-byte file> -w %{size_upload}`):
  `WS-ENC: sending [BIN payload=0/65536]` x3 then `[BIN payload=0/3392]`; request.bin 200 248 bytes = 198-byte
  request + 3 x (14 + 65 536) + (8 + 3 392); `upload completely sent off: 200050 bytes`; `%{size_upload}` 200050.
  Empty file: no frame, no `WS-ENC` line, `We are completely uploaded and fine`, `%{size_upload}` 0.
- Decision (ADR-0670): every non-raw upload goes through the `cr_ws_read` loop; a non-callback upload fills each
  64 KiB read (as `fread` does on a file or pipe) before framing it; a zero-byte upload writes
  `We are completely uploaded and fine`. Raw mode keeps the whole, unframed send.
- Pinned by `WsProtocolHandlerFramedUploadTests.ExecuteAsync_ToolUploadOf200000Bytes_SendsCurlsFramesOf65536And3392Bytes`,
  plus `..._ToolUploadReadInParts_FillsEachFrameTo65536Bytes` and `..._EmptyToolUpload_SendsNoFrameAndSaysItIsCompletelyUploaded`.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 9a4e158c 53bb8082.
- 2026-10-17: Doing -> Done. A ws -T upload goes out in one binary frame per filled 64 KiB read, as curl 8.22.0 (200 000 bytes = 65536x3 + 3392); an empty one sends no frame
