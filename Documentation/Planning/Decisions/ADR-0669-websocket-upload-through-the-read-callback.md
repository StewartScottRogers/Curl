# ADR-0669 — A WebSocket upload through the read callback goes out read by read, and `curl_ws_start_frame` frames it

- **Status:** Accepted
- **Date:** 2026-10-17

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2611.

## Context

ADR-0665 (BL-2481) built `curl_ws_start_frame` for a `CURLOPT_CONNECT_ONLY` 2 connection only. A `ws://` or
`wss://` perform with `CURLOPT_UPLOAD` that is not connect-only read its whole upload and sent it as one binary
frame (ADR-0131), so a frame started from its read callback had nowhere to go.

curl 8.22.0's `lib/ws.c` (read from the release's source; a libcurl read callback cannot be driven from the
command line, so it is not measured) does this once the `101` arrives with `CURLOPT_UPLOAD` set and
`CURLWS_RAW_MODE` clear: `Curl_ws_accept` adds the `cr_ws_read` client reader, which for each read

- asks the next reader (the read callback) for at most the buffer it was given, or for no more than
  `ws->enc.payload_remain` while a frame has payload left;
- if the callback called `curl_ws_start_frame`, whose head `ws_enc_write_head` left in `ws->sendbuf`, sends that
  head and the bytes read as that frame's payload (`ws_enc_write_payload` writes no more than the payload left; the
  rest of the read is dropped), and a read of nothing still sends the head;
- otherwise encodes the bytes read as one new `CURLWS_BINARY` frame of their length, and a read of nothing with no
  head waiting ends the upload.

`curl_ws_start_frame` needs only the connection's WebSocket state, so it works on such a transfer;
with no WebSocket state it is `CURLE_SEND_ERROR`, as is a frame still unfinished.

## Decision

1. `Curl.Original.Protocol.Ws` gains `WsUploadFrames` (the upload's `ws->enc` and `ws->sendbuf`) and `IWsUploadFrameSource`.
   `WsProtocolHandler` sends an upload stream that implements it with non-null frames read by read, as `cr_ws_read`
   does above, each read asking for up to 64 KiB (`UploadReadSize`, libcurl's default `CURLOPT_UPLOAD_BUFFERSIZE`).
   The frames are open only while the upload is sent. Under `RawMode` the upload still goes out unframed (BL-2526).
2. The engine's `CallbackReadStream` implements it, and a perform from the options gives its read-callback upload
   the handle's `ManagedEasyWebSocket.UploadFrames` (one per handle, as `ws->enc` is one per connection).
3. `ManagedEasyWebSocket.StartFrameAsync` on a handle that is not connect-only starts the frame there:
   `CURLE_FAILED_INIT` under raw mode, `CURLE_SEND_ERROR` for a negative length, no upload being sent or a frame
   unfinished, `CURLE_BAD_FUNCTION_ARGUMENT` for bits naming no frame, else `CURLE_OK`. The connect-only behaviour
   of ADR-0665 is unchanged.
4. An upload that is not a read callback's - the curl tool's `-T` file or standard input, which the tool hands the
   engine as its own stream - keeps ADR-0131's one frame. For an upload that fits one 64 KiB read the bytes are the
   same; a larger `-T` would be several frames in curl, and is left to its own task.
5. Each binary frame the reader starts writes the `--trace-config ws` `WS-ENC` lines and the `ws` log's `sent`
   line; a frame the callback started writes neither. `upload completely sent off` gives the bytes sent in all.

## Consequences

- A libcurl program that uploads over a WebSocket through its read callback sends the frames `lib/ws.c` sends, and
  one that calls `curl_ws_start_frame` from the callback sends text, continuation or control frames of its choosing.
- The tool's `-T` on a WebSocket is unchanged.
