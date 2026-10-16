# ADR-0585 — Libcurl's curl_ws_recv, curl_ws_send, curl_ws_meta and curl_ws_start_frame

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2445.

## Context

BL-2445 asks `CurlEasy` for libcurl's WebSocket API (`include/curl/websockets.h`). The seam already has it:
`IEasyTransfer.WebSocket` is an `IEasyWebSocket` (`ReceiveAsync`, `SendAsync`, `StartFrameAsync`,
`CurrentFrame`) with `CurlWebSocketFlags` and the result records (BL-2315). The managed engine does not
implement it yet (BL-2443, BL-2444), so the Libcurl side is tested over fakes. The task's `touches` are
`Libcurl/WebSocket` and its tests, not `Libcurl/Easy`, and `CurlEasy` is `sealed`, not `partial`.

## Decision

1. The calls are extension methods on `CurlEasy` in `Libcurl.WebSocket.CurlEasyWebSocket`, so the area
   lives in its own folder without editing `CurlEasy.cs`: `WebSocketRecv`, `WebSocketSend`,
   `WebSocketMeta` and `WebSocketStartFrame`, each (but `Meta`) with an `Async` form that is the native
   shape. The blocking forms block on the async ones, as `Perform` and `Upkeep` do; libcurl's calls are
   synchronous (and non-blocking on the wire, answering `CURLE_AGAIN`), so the engine's `ValueTask`s are
   expected to complete without waiting.
2. The blocking forms keep libcurl's out parameters: `WebSocketRecv(buffer, out received, out meta)` and
   `WebSocketSend(buffer, out sent, fragmentSize, flags)`, in `curl_ws_send`'s argument order.
   `curl_ws_recv` sets `*nread` to 0 and `*metap` to `NULL` first and fills them only on success, so
   `WebSocketRecv` answers 0 and `null` unless the code is `Ok`. `WebSocketSend` passes the engine's
   `BytesSent` through on any code, as `curl_ws_send` reports a partial send.
3. A `null` or cleaned-up handle fails libcurl's `GOOD_EASY_HANDLE` check: every call answers
   `BadFunctionArgument` (with no bytes) and `WebSocketMeta` `null`, without calling the engine. This
   differs on purpose from `CurlEasy`'s own members, which throw `ObjectDisposedException`: an extension
   method can be called on `null`, and libcurl answers a code for both cases.
4. Every other check - not a WebSocket connection, raw mode, a send outside a frame - is the engine's,
   behind `IEasyWebSocket`; Libcurl passes the engine's code through unchanged.

## Consequences

- `CurlEasy` gains the four calls without a `partial` split; a later area that needs members on `CurlEasy`
  itself may still make it `partial`.
- `Libcurl` now has four sanctioned sync-over-async bridges (`Perform`, `Upkeep`, and the blocking
  WebSocket calls), each a libcurl blocking call over its `Async` form.
