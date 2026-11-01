# ADR-1041 — A ws upload can give whole messages, one frame each

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4253.

## Context

`WsProtocolHandler` frames any upload that is not an `IWsUploadFrameSource` into binary frames of up to 64 KiB, as
curl 8.22.0's `cr_ws_read` does, so several messages written to the upload become one binary frame.
graphql-transport-ws, WAMP's `wamp.2.json` and STOMP over WebSocket (BL-4114) need one text frame per message. The
per-frame seam `IWsUploadFrameSource` lives in `Curl.Original.Protocol.Ws`, which an extension may not reference (ADR-0999),
and the engine uses it for `curl_ws_start_frame`.

## Decision

- `Curl.WebSocket.UnitLibrary` gains `IWsUploadMessageSource` (an upload stream that also gives
  `IReadOnlyList<WsUploadMessage> Messages`) and `WsUploadMessage(WsOpcode Opcode, ReadOnlyMemory<byte> Payload)`.
  A list, not an async sequence: the extensions this serves know their messages before the transfer starts, and a
  list keeps the handler's loop simple.
- When the upload is an `IWsUploadMessageSource` and `RawMode` is off, the handler sends each message as one final
  masked frame of its opcode with `WsFrameEncoder` and its random source, in order, then receives as before. The
  stream's own bytes are not read. Each frame is traced and logged as the read-by-read path traces its frames.
- Under `RawMode` the stream's bytes go out unframed, as any other upload does: raw mode means no framing.
- `%{size_upload}`, the request size and the upload progress count the sum of the message payloads, as BL-4253's
  criteria ask, not the frame bytes the read-by-read path counts: an extension reports what it uploaded.
- Every other upload is framed as before, so no curl behaviour or upstream case changes.

## Consequences

An extension sets its upload to a stream implementing `IWsUploadMessageSource` to send one frame per message.
`IWsUploadFrameSource` stays where it is for libcurl's `curl_ws_start_frame`.
