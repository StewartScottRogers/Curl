# ADR-0832: A dialer publishes each socket it creates before connecting it

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3890
- Decided by Claude under Stewart's delegation.

## Context

A transfer in a multi handle driven by `curl_multi_socket_action` must give the socket callback its
real OS socket with `CURL_POLL_OUT` while the TCP connect is in progress: curl 8.22.0 answers a refused
connect with `T0 S=2 T-1 S=4` and result 7 (CGR-034 case `C_65`). `TcpDialer` creates the `Socket` and
connects it in one call, so `SocketActionGatedTcpDialer` learned the socket only once the dial returned,
and never for a connect that failed.

## Decision

`ITcpDialer` gains `PublishingSocketsTo(Action<nint> socketCreated)`, a default interface member that
returns the dialer itself and hands nothing, for dialers that create no real socket (every fake and
wrapper keeps compiling unchanged). `TcpDialer` overrides it with a new `TcpDialer` over the same
`SocketOptions` that hands the hook each socket's handle once its options are set and before it
connects (`PrepareToConnect`, `PublishCreated`), the Unix domain socket's included.

`SocketActionGatedTcpDialer` dials through `inner.PublishingSocketsTo(wait.ConnectingOn)`, and
`TransferSocketWait.ConnectingOn` sets `TransferSocketWait.Socket`, so `Waiting` is `CurlPoll.Out` on a
known socket during the connect, and the handle stays known after a refused connect.

## Why

- A per-dial view, not a mutable hook on the shared dialer: each socket-action transfer gets its own
  view, so concurrent transfers never publish into each other's wait, and no `AsyncLocal` is needed.
- A default interface member, so the hook is optional and costs the many test dialers nothing.
- The handle, not the `Socket`, crosses the seam: the engine only reports it, and protocol and engine
  code still never hold a `Socket` (CLAUDE.md's networking rule).
- Two attempts of a happy-eyeballs race publish in turn; the last one created is the one reported,
  which `BL-3563`'s wiring then replaces with the connected socket's handle in `EndConnect`.

## Consequences

`ManagedMultiTransfer`'s socket callback (BL-3563's wiring) can report the socket from the start of the
connect. BCL only and AOT-safe: no reflection, one delegate per dial.
