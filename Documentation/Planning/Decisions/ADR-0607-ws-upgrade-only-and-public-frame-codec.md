# ADR-0607 — Ws: an upgrade-only connect and a public frame codec for libcurl's WebSocket API

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2471.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` (ADR-0506) needs `curl_ws_recv`,
`curl_ws_send` and `curl_ws_meta` over a transfer run with `CURLOPT_CONNECT_ONLY` 2. Until now
`Curl.Original.Protocol.Ws` only ran the whole tool transfer: `WsProtocolHandler.ExecuteAsync` read frames
until the server closed, `WsFrameEncoder` wrote only FIN frames, and the decoder merged every payload
into one run with no frame metadata, all `internal`. The Ws library may reference only
`Curl.Protocol.Abstractions`, so it cannot use `Curl.Api`'s `CurlWebSocketFrame` or
`CurlWebSocketFlags`.

## Decision

1. **Upgrade-only connect.** `WsProtocolHandler.ConnectAndUpgradeOnlyAsync` runs the same connect and
   upgrade code as `ExecuteAsync` - same request bytes, `-v` lines and refusals - and on a `101` returns
   a `WsUpgradeOutcome` holding the open connection, which the caller then owns, and the undecoded
   bytes that came with the reply head. After the head it writes curl 8.21.0's `Received 101` lines and
   the `--trace-config ws` chunk-size line, as for every accepted upgrade, and stops there: no
   `websocket established, callback mode` line (that names callback mode, which connect-only is not),
   no transfer-done progress and no diagnostic-log transfer end, because the transfer goes on through
   the engine. A failure closes the connection, is logged as the transfer's end, and carries the same
   exit code and message as the tool's transfer.
2. **Public encoder.** `WsFrameEncoder.Encode(opcode, isFinal, payload, randomSource)` writes a masked
   frame of any opcode with the FIN bit as asked; a fragmented message is a first `Text` or `Binary`
   frame with FIN clear, then `Continuation` frames, the last with FIN set. The tool's own frames go
   through the same code with FIN set, so their bytes are unchanged.
3. **Public decoder with metadata.** `WsFrameMetadataDecoder` wraps the tool's decoder made to record
   runs, so the checks, their order and the exit 56 messages are the tool's own. Each call returns
   `WsDecodedFrames`: every run of payload as a `WsFrameRun` (opcode, FIN, offset in the frame, bytes
   left, payload) - ping, pong and close frames included, and a frame with no payload reported once
   with an empty run - the last completed ping for auto-pong, and the violation message, if any.
4. **The library's own types.** `WsOpcode` becomes public; the engine maps it and the FIN bit to
   `CURLWS_TEXT`, `BINARY`, `CONT`, `CLOSE`, `PING` and `PONG`.

## Consequences

The tool's behaviour is byte-identical: every existing Ws test passes unchanged. BL-2443 builds
`IEasyWebSocket` on these three pieces. Whether curl's connect-only mode writes any further `-v` or
trace line after the `101` is not measured here; the engine task pins that against a libcurl build if
it differs.
