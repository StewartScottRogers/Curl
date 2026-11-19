# ADR-1308 — A Libcurl port waits for a WebSocket frame with WebSocketWaitAsync

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5554).

## Context
`curl_ws_recv` answers `CURLE_AGAIN` when no frame is ready. A C program waits on `CURLINFO_ACTIVESOCKET` with
`select` or `poll`. In Curl the connect-only socket number is read by the engine's own pending read, so a second wait
on it (`CurlMulti.WaitAsync`) would start a competing read on the same connection and could lose bytes; and .NET has
no `select` on a bare number. A port had only a sleep.

## Decision
`IEasyWebSocket` gains `WaitForDataAsync(CancellationToken)`, a default interface member that completes at once, so
existing fakes still compile. `ManagedEasyWebSocket` overrides it: it starts (or reuses) the receive's own pending read
and completes when that read does, or at once when a run is queued, the stream failed or there is no usable
connection; cancellation throws `OperationCanceledException`. `CurlEasyWebSocket.WebSocketWaitAsync` exposes it.
It receives nothing: `WebSocketRecv` still answers `Again` when no frame is ready. `Libcurl/README.md` shows the loop.

## Why
It reuses the one read the receive already owns, so no bytes are lost, and adds no package or option.
