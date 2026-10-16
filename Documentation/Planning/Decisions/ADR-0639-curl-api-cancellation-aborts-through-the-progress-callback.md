# ADR-0639 — `Curl.Api` cancellation aborts through the transfer-info callback and throws `OperationCanceledException`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2504.

## Context

Every `CurlClient` call takes a `CancellationToken`. Before BL-2504 the token was only passed to
`IEasyTransfer.PerformAsync`, and a cancelled transfer surfaced as `CurlException` with the engine's
error code, so a caller could not tell its own cancellation from a failure, and a timeout
(`CURLE_OPERATION_TIMEDOUT`) looked the same as a cancel. A libcurl-backed engine cannot honour a
.NET token at all: libcurl stops a transfer only when a callback says so, and a paused libcurl
transfer calls nothing until it is unpaused.

## Decision

- `TransferCancellation` (`Curl.Api/Cancellation`) carries the token into a transfer the way
  libcurl sees it: it sets the handle's `CURLOPT_XFERINFOFUNCTION` and turns `CURLOPT_NOPROGRESS`
  off; the callback answers 0 (carry on, no meter) until the token is cancelled, then 1, which
  aborts with `CURLE_ABORTED_BY_CALLBACK`. On cancellation it also unpauses the handle
  (`curl_easy_pause(CURLPAUSE_CONT)`), first letting the owner change its answer
  (`ResponseBodyStream` aborts its buffer, so the redelivered data is refused rather than paused
  again). The token still goes to `PerformAsync` too, for engines that honour it directly.
- A transfer that fails after the caller's token was cancelled throws `OperationCanceledException`
  carrying that token, whatever code it ended with; any other failure, a timeout included, throws
  `CurlException`. The buffer decides when the transfer ends, so a token cancelled after a timeout
  still reads as the timeout.
- A token already cancelled throws `OperationCanceledException` before any handle is created.
  Every other path frees the handle before the exception reaches the caller (`SendAsync`,
  `ConnectWebSocketAsync`), or when the body is disposed after it (a cancel while the body streams;
  the body's next read throws once what was buffered is read).
- Disposing the body early uses the same path, so a paused transfer is unpaused and ends at once
  (with `CURLE_WRITE_ERROR` from the refused redelivery) instead of waiting on the perform token.

## Consequences

- Tests drive `CancellableEasyTransfer`, a fake that ignores the perform token and stays still while
  paused, so they prove the callback path alone stops connecting, uploading, downloading and paused
  transfers.
- The WebSocket left open after `ConnectWebSocketAsync` keeps the transfer-info callback, which
  answers 0 forever once the connect is done.
