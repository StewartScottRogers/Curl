# ADR-0599 — The managed engine's multi handle: socket action, assign, and the socket and timer callbacks

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2426.

## Context

ADR-0594 and ADR-0597 gave `ManagedMultiTransfer` perform, info_read, timeout, wait, poll and wakeup,
and left `curl_multi_socket_action` and `curl_multi_assign` throwing `NotSupportedException`. libcurl's
socket-action API hands the application the operating system descriptors of its connections through
`CURLMOPT_SOCKETFUNCTION` and a single timeout through `CURLMOPT_TIMERFUNCTION`, and the application
calls `curl_multi_socket_action` when a descriptor is ready or the timer expires. The managed engine
runs each transfer as one asynchronous `IEasyTransfer.PerformAsync` over `IConnection`s (ADR-0594):
it has no descriptor an application could wait on, and a transfer moves on whether the application
calls in or not.

## Decision

1. **One socket per running transfer.** The first action after a transfer starts gives it a socket
   numbered by the handle, from `0x40000000` upward (`ManagedMultiTransfer.FirstTransferSocket`, above
   any descriptor a process is likely to hold), and tells the socket callback `(transfer, socket,
   CURL_POLL_IN, null)`. The first action after it finishes tells it `CURL_POLL_REMOVE` with the value
   `Assign` gave, and the socket is forgotten. A transfer that finishes before its first action is
   never given one. `Remove` tells the callback `CURL_POLL_REMOVE` for a transfer that has a socket, and
   does not check its answer.
2. **The timer drives progress.** The socket never becomes readable to the application's poll, so
   while a transfer runs the timer asks to be called back in at most 10 ms
   (`RunningTransferCheckInterval`), sooner when `curl_multi_timeout` (ADR-0597) is sooner; with nothing
   running it is `curl_multi_timeout` itself (0 for a transfer not yet started, else -1, which deletes
   the timer). The callback is told only when the value changed, or after an action on
   `CURL_SOCKET_TIMEOUT` whenever a timer is still wanted, since that timer is spent. `Add` tells it 0,
   as libcurl's `curl_multi_add_handle` does.
3. **One loop for both entry points.** `SocketActionAsync` and `PerformAsync` both start every transfer
   not yet started, queue each finished one's `CURLMSG_DONE` and then update the socket and timer
   callbacks. The socket and events given to `SocketActionAsync` are not read: every transfer runs on
   its own, so there is nothing more for one socket's event to move on.
4. **A callback's -1 kills the handle.** As libcurl's dead multi handle does, a socket or timer
   callback returning -1 makes the call answer `CURLM_ABORTED_BY_CALLBACK`; from the next action on,
   every unfinished transfer is cancelled (one not started never starts) and queued as done with
   `CURLE_ABORTED_BY_CALLBACK`.
5. **Assign.** `Assign` sets the value for a socket the callback was told of and not yet told to
   remove; any other socket is `CURLM_BAD_SOCKET`, and a disposed handle `CURLM_BAD_HANDLE`.

## Consequences

- An application written for libcurl's socket-action API works unchanged as long as it honours the
  timer callback, which every libcurl event-loop example does. One that hands the socket to `epoll`,
  `kqueue` or libuv without checking the result gets an error there; `Libcurl` (BL-2431) decides
  what its C-shaped surface does with these numbers.
- Progress is noticed within 10 ms of a transfer finishing, at the cost of a timer every 10 ms while
  transfers run.
- Tests drive a transfer purely through socket actions with fake transfers and pin the exact
  sequence of socket and timer calls (`ManagedMultiTransferTests.SocketAction.cs`).
