# ADR-0665 — The engine sends WebSocket frames in parts: `CURLWS_OFFSET` and `curl_ws_start_frame`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2481.

## Context

ADR-0616 (BL-2443) built `curl_ws_recv`, `curl_ws_send` and `curl_ws_meta` over a `CURLOPT_CONNECT_ONLY` 2
connection and answered `CURLE_BAD_FUNCTION_ARGUMENT` for `CURLWS_OFFSET` and `curl_ws_start_frame` (its point 8).
`Curl.Original.Protocol.Ws`'s `WsFrameEncoder` wrote only whole frames.

curl 8.22.0's `lib/ws.c` (read from the release's source; the curl tool sends no frame in parts, so it cannot be
measured from the command line) does this:

- `curl_ws_send` outside raw mode: while the frame being sent has payload left (`ws->enc.payload_remain`), the call
  continues it whatever its flags, and a `buflen` past what is left is `CURLE_BAD_FUNCTION_ARGUMENT`
  ("unaligned frame size"). Otherwise it writes a new head with `ws_enc_write_head`, announcing `fragsize` under
  `CURLWS_OFFSET` and `buflen` otherwise, then the payload, each byte masked by its position in the frame.
- `ws_enc_write_head`: a negative length is `CURLE_SEND_ERROR`, a frame with payload left is `CURLE_SEND_ERROR`
  ("starting new frame with ... remaining"), and bits naming no frame (`CURLWS_OFFSET` ignored) are
  `CURLE_BAD_FUNCTION_ARGUMENT`.
- `curl_ws_start_frame`: `CURLWS_RAW_MODE` is `CURLE_FAILED_INIT`; no connection, a previous frame unfinished
  (`[WS] previous frame not finished`) or the head's own refusals are `CURLE_SEND_ERROR` (bits:
  `CURLE_BAD_FUNCTION_ARGUMENT`); otherwise the head is queued and the sends that follow supply the payload. It
  checks no callback: the documentation says to call it from a read callback, but the code behaves the same
  outside one.

## Decision

1. `WsFrameEncoder` gains `EncodeHead` (a head announcing a `long` payload length, ending with the mask drawn from
   the random source) and `MaskPart` (one part masked at its offset in the payload). `Encode` is built from them,
   so a whole frame and the same frame in parts are the same bytes.
2. `ManagedEasyWebSocket` keeps the frame being sent (`sendFrameLeft`, `sendFrameOffset`, its mask) and applies the
   rules above with the same codes. A first part longer than `fragsize` is `CURLE_BAD_FUNCTION_ARGUMENT`, the
   unaligned-size answer, as libcurl's payload writer never sends more than the frame announces.
3. `StartFrameAsync` writes the head at once rather than queueing it: the bytes on the wire are the same, and the
   engine flushes every send anyway. No connection is `CURLE_SEND_ERROR`, as `curl_ws_start_frame` says, though
   `curl_ws_send` with none stays `CURLE_BAD_FUNCTION_ARGUMENT` (ADR-0616).
4. Inside and outside a read callback the call behaves the same, as libcurl's does. The engine runs WebSocket
   transfers only connect-only, where no read callback is called, so a frame started from a WebSocket upload's
   read callback waits on the engine running such uploads at all.
5. Closing or replacing the connection forgets an unfinished frame.

## Consequences

- ADR-0616's point 8 no longer holds; the rest of it stands.
- `Libcurl`'s `curl_ws_send` with `CURLWS_OFFSET` and `curl_ws_start_frame` now reach working engine code.
