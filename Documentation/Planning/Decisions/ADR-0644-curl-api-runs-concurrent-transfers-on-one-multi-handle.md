# ADR-0644 — `Curl.Api` runs concurrent transfers on one multi handle per client

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2509.

## Context

Before BL-2509 each `CurlClient.SendAsync` call performed its own easy handle
(`IEasyTransfer.PerformAsync`), so calls made together shared nothing: no connection limit, no
per-host limit, and no single place a disposing client could stop them. Phase 4 of
`Documentation/Planning/Curl-Api-And-libcurl.md` maps concurrency onto the seam's multi contract
(`ICurlEngine.CreateMulti`, `IMultiTransfer`, `MultiOptions`), which is how libcurl applications run
transfers together and the only place libcurl applies `CURLMOPT_MAX_TOTAL_CONNECTIONS` and
`CURLMOPT_MAX_HOST_CONNECTIONS`.

## Decision

- Every `SendAsync` transfer of a client runs on that client's one multi handle through
  `MultiTransferDriver` (`Curl.Api/Concurrency`). The handle is created on the client's first
  transfer (a client that never sends creates none) and disposed with the client.
- One driver loop per client, started by the first transfer and ended once none is left: it applies
  queued adds and removes, calls `PerformAsync`, takes each `CURLMSG_DONE` message to finish its
  transfer (removing it from the handle), and waits in `PollAsync` (at most one second), which
  `Wakeup` ends when a transfer arrives or is cancelled. Only the loop calls the multi handle, so it
  is never used from two threads, as libcurl requires; `Wakeup` is the one thread-safe call. No
  thread is blocked while waiting.
- `CurlClientOptions.MaxConnections` and `MaxConnectionsPerHost` set
  `CURLMOPT_MAX_TOTAL_CONNECTIONS` and `CURLMOPT_MAX_HOST_CONNECTIONS` on the handle; both default to
  0, libcurl's "no limit", and a negative value throws `ArgumentOutOfRangeException`. The engine holds
  a transfer past a limit pending, as libcurl does.
- Each call completes or fails on its own: a transfer's error code fails only its call. A cancelled
  call's transfer is removed from the handle (`curl_multi_remove_handle`) as well as aborted through
  the progress callback (ADR-0639), and ends with `CURLE_ABORTED_BY_CALLBACK`, thrown as
  `OperationCanceledException`. A failed `curl_multi_add_handle` fails its call with
  `InvalidOperationException`; a failed perform or poll, or an exception out of the perform, fails
  every running call, since it does not say which transfer it was about.
- Disposing the client cancels a token every transfer's body is linked to, so every running call,
  or the next read of a body still streaming, throws `OperationCanceledException` carrying that
  token; the driver then stops what is left, disposes the multi handle, and the engine is disposed
  last when owned.

## Consequences

- The tests' fake engines that send return `EasyPerformingMultiTransfer`, a fake multi handle that
  runs each added transfer by its own perform and honours both limits, so the earlier `SendAsync`
  tests run unchanged over the driver.
- A client disposed while a body is still being read now aborts that transfer; before, the body
  kept running on its own.
- `ConnectWebSocketAsync` still performs its handle directly: a `CURLOPT_CONNECT_ONLY` transfer is
  over once connected and is not part of the concurrency the multi handle provides.
