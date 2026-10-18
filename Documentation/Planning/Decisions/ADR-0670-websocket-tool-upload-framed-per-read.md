# ADR-0670 — A `-T` WebSocket upload goes out in one binary frame per filled 64 KiB read

- **Status:** Accepted
- **Date:** 2026-10-17

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2683.

## Context

ADR-0131 sent the whole of a `-T` upload on a `ws://` transfer as one binary frame. ADR-0669 (BL-2611) moved a
libcurl read callback's upload to `cr_ws_read`'s read-by-read framing and left every other upload on one frame.

Measured with the reference curl 8.22.0 through `Record-CurlExchange.ps1` (a loopback server answering `101`),
`curl -sS -v --trace-config ws -T <file> -w '%{size_upload}\n' ws://127.0.0.1:<port>/`:

- a 200 000-byte file: `WS-ENC: sending [BIN payload=0/65536]` three times, then `[BIN payload=0/3392]`; the
  bytes after the request are frames of 65 536, 65 536, 65 536 and 3 392 bytes of payload (heads of 14, 14, 14
  and 8 bytes); `upload completely sent off: 200050 bytes`; `%{size_upload}` 200050.
- an empty file: nothing after the request, no `WS-ENC` line, `We are completely uploaded and fine`;
  `%{size_upload}` 0.

The tool's read callback is `fread`, which fills its buffer from a file or a pipe until the end, so each read
`cr_ws_read` frames is a full 64 KiB until the last.

## Decision

`WsProtocolHandler` sends every upload that is not under `CURLWS_RAW_MODE` through the `cr_ws_read` loop of
ADR-0669. An upload that is not a read callback's (`IWsUploadFrameSource` absent or with no `WsUploadFrames`) gets
a fresh `WsUploadFrames`, and each of its reads is filled to `UploadReadSize` (64 KiB) or the end of the upload
before it is framed, as `fread` fills it; a read callback's reads are framed as they come, as before. An upload
that sends no bytes writes `We are completely uploaded and fine` in place of `upload completely sent off`.
Under `CURLWS_RAW_MODE` the upload still goes out whole and unframed (BL-2526).

## Consequences

- Uploads under 64 KiB send the same bytes as before; larger ones match curl's frames, trace lines and
  `%{size_upload}`.
- An empty `-T` upload no longer sends a zero-length binary frame.
- ADR-0131's one-frame rule no longer holds for any upload.
