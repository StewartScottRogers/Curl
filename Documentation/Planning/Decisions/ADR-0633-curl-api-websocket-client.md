# ADR-0633 — `Curl.Api`'s WebSocket client: whole messages over `curl_ws_recv`, a short wait after `CURLE_AGAIN`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2508.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md` asks for a WebSocket client in
`Curl.Api` with `IAsyncEnumerable` messages. The seam is `IEasyWebSocket` on an easy handle performed
with `CURLOPT_CONNECT_ONLY` 2. Its `ReceiveAsync` and `SendAsync` never wait, as `curl_ws_recv` and
`curl_ws_send` do not: the managed engine answers `CurlCode.Again` while its read runs or only part
of a frame has come (ADR-0616). libcurl leaves waiting to the application (it polls the socket);
the seam gives `Curl.Api` no socket to wait on.

## Decision

- `CurlClient.ConnectWebSocketAsync(url[, CurlWebSocketOptions], cancellationToken)` creates an easy
  handle with the client's defaults, sets `CURLOPT_URL`, `CURLOPT_CONNECT_ONLY` 2 and
  `CURLOPT_WS_OPTIONS` (`AutoPong = false` is `CURLWS_NOAUTOPONG`, `RawMode` is `CURLWS_RAW_MODE`)
  and performs it. A failed perform disposes the handle and throws `CurlException` with the handle's
  error text, else the `strerror` text (ADR-0630); a perform that throws is rethrown after the dispose.
- `CurlWebSocket` owns the handle. A receive or send answered `Again` waits 1 ms on the system clock
  (`Task.Delay` with `TimeProvider.System`, honouring the token) and asks again; any other failure is
  a `CurlException` with the `strerror` text. The wait is injected for tests.
- `ReceiveAsync` gives whole messages (`CurlWebSocketMessage`: type, payload, `Text`, `CloseStatus`,
  `CloseReason`): text and binary fragments are joined until a piece with no `CURLWS_CONT` and no
  bytes left; a ping, pong or close frame is its own message and may arrive between fragments
  without losing them. A data frame with neither `CURLWS_TEXT` nor `CURLWS_BINARY` is binary.
- `ReadMessagesAsync` enumerates messages through the peer's close frame, which is the last one
  given; its token and `WithCancellation`'s are linked.
- Sends: `SendTextAsync`, `SendAsync(data, type)`, `PingAsync`, `PongAsync`, `CloseAsync(status = 1000,
  reason)` (the status big-endian then the UTF-8 reason, at most 123 bytes), `SendRawAsync` (no bits,
  for raw mode) and the frame API `SendFrameAsync(data, flags)`; a partial send sends the rest with
  the same bits. `ReceiveFrameAsync(buffer)` and `CurrentFrame` (`curl_ws_meta`) are the frame API
  for receiving.
- `curl_ws_start_frame` is not exposed: it is valid only inside a read callback of a WebSocket
  upload, which a connect-only WebSocket has none of.

## Why

A 1 ms retry keeps latency low without a socket to wait on and without changing the seam, which
other tasks share; the engine starts its read on the first `Again`, so the wait only spaces the
checks. Ending the enumeration at the close frame matches what a caller wants from `await foreach`
and avoids the `CURLE_GOT_NOTHING` the next receive would throw once the peer closes the TCP
connection.
