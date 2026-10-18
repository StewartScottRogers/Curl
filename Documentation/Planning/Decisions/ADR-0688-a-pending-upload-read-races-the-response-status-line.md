# ADR-0688 — A pending upload read races the response's status line

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2729.

## Context

Upstream case 1533 posts a 3-byte body whose read callback returns `CURL_READFUNC_PAUSE` until the write
callback has seen the response and calls `curl_easy_pause(CURLPAUSE_CONT)`. The server answers `401` with a
15-byte body before the request body arrives. libcurl 8.x sends and receives in one loop, so it delivers the
response while the upload is paused; on a status of 300 or above it stops sending and closes the connection,
unless `CURLOPT_KEEP_SENDING_ON_ERROR` is set, when it sends the rest once unpaused and keeps the connection.

Curl's HTTP handler sends the whole body before it reads the response head, so a read the callback keeps
pending is never resumed and the transfer hangs. The HTTP library cannot tell a paused callback read from a
slow one (`-T -` from a pipe): both are a `Stream.ReadAsync` that does not complete at once.

## Decision

Over HTTP/1.x, a request with a stream body sent without waiting for `100 Continue` reads its response through
`HttpEarlyResponseConnection`. When a body read does not complete at once, `HttpRequestBodyWriter` sends the
request head (and anything held back), then waits for whichever comes first: the read, or the response's first
line, which the connection buffers and replays to the head reader.

- The read first, or a status below 300, or no status line: the writer waits for the read and goes on as before.
  A slow stdin upload therefore behaves as it did, since the server sends nothing until the body arrives.
- A status of 300 or above first, without `HttpRequestOptions.KeepSendingOnError`: the read is cancelled, the
  body is cut short with nothing sent, and the connection is not kept.
- The same with `KeepSendingOnError`: the read stays pending, the response is read and delivered, and then the
  writer waits for the read and sends the rest of the body (`WriteRestAfterResponseAsync`); the connection is
  kept when the response allows it.

HTTP/2 and HTTP/3 streams and bodies of bytes are unchanged.

## Consequences

- Case 1533's wire bytes are reachable once the engine maps `CURLOPT_KEEP_SENDING_ON_ERROR` (BL-2646).
- A request with a stream body allocates a small replay buffer for the status line.
- The `--trace-config read` lines of a read resumed after the response count the whole upload buffer as its
  room; no measured case pauses a traced upload.
