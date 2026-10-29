# ADR-0970: A socket-action transfer reports its real socket, OUT then IN, and its deadline as the timer

- Status: Accepted
- Date: 2026-10-28
- Decision: Decided by Claude under Stewart's delegation (BL-3563, BL-4016, CGR-034)

## Context

CGR-034: `curl_multi_socket_action` gave the socket callback engine numbers from `0x40000000`, always
`CURL_POLL_IN`, and re-armed the timer every 10 ms while a transfer ran, so an application's own event loop
(libuv, libevent, epoll) could not poll the socket and only finished transfers by busy-polling the timer.
curl 8.22.0 gives `add T0 kick S=2 T-1 S=1 S=4` for one transfer, `T0 S=2 T400+ S=1 S=4 T-1` with
`CURLOPT_TIMEOUT_MS` 600 against a silent server, and `T0 T-1` with only a timer callback driven by perform.
BL-3846 put the OS socket on `IConnection.SocketHandle`, BL-3847 built `TransferSocketWait` (ADR-0821's
companion design) and BL-3859 routed a transfer's dials through it (ADR-0822).

## Decision

- `ManagedMultiTransfer` gives each `ManagedEasyTransfer` it starts a `TransferSocketWait` while the
  multi handle has a socket callback (`SocketWaitFor`); without one there is no wait and perform drives the
  transfer as before. Other `IEasyTransfer`s keep the engine-numbered socket and 10 ms timer (ADR-0599).
- `SocketActionGatedTcpDialer` passes the connection's `SocketHandle` to `TransferSocketWait.EndConnect`,
  which keeps it as `Socket`.
- `SocketActionAsync` signals the wait whose socket the action names with the action's events (with none,
  the directions it waits in); `PerformAsync` signals every wait with the directions it waits in, as
  libcurl's perform checks every socket itself.
- After moving the transfers on, the call lets each such transfer go on until it waits on its socket again
  or ends, running the callbacks it queues (CGR-033) meanwhile, for at most `SocketWaitSettleLimit`
  (200 ms of real time, as `MultiCallbackQueue.FollowUpWait` is), then queues the done messages of the
  transfers started before the call. That is the work libcurl's action does on the calling thread, so the
  callbacks that follow say what the transfer waits for next.
- The socket callback is told the real socket and `TransferSocketWait.Waiting` (`Out` while a write
  waits, `In` while a read waits) whenever either changed, and `Remove` when the transfer ends.
- The timer callback is the handle's timeout when no transfer runs or no socket callback is set; when every
  running transfer waits on its socket, the earliest `EarliestDeadline` (-1 when none); otherwise at most
  `RunningTransferCheckInterval`. A positive value is not told again while its due time moves by at most a
  millisecond, so a deadline is told once, as libcurl's.

Once a transfer finishes, is removed or its multi handle is cleaned up, its `TransferSocketWait` stops gating
(`StopGating`): the waits in progress return and every later read or write on the connection passes at once. The
connection outlives the transfer in the pool, and cleanup closes it (an SFTP session sends its channel close) when
the application sends no more socket actions; without this, upstream test582 hung in `curl_multi_cleanup` (BL-4016).

## Consequences

- One transfer, the silent-server timeout and the timer-only perform match curl 8.22.0 (`Cgr034Tests`).
- The connect itself is not on a socket the application can see: `Curl.Networking`'s dialer creates and
  connects the socket inside one call, so a refused connect (CGR-034's `C_65`, curl `T0 S=2 T-1 S=4`) gives
  no socket callback and the 10 ms timer until it ends. Reporting the socket while it connects is follow-up
  work.
