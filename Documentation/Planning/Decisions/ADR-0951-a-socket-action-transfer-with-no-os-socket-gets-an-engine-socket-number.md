# ADR-0951: A socket-action transfer with no operating system socket gets an engine socket number

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3981 (CGR-034)
- Decided by Claude under Stewart's delegation.

## Context

A transfer in a multi handle driven by `curl_multi_socket_action` waits on its `TransferSocketWait`, and the
multi handle tells the application of its socket (`TransferSocketWait.Socket`). The dialer sets that socket as it
creates an operating system socket (`ConnectingOn`), but a connection with none - the conformance harness's
in-memory dialing, `IConnection.SocketHandle` null - left it unset, so the application was never told of a socket
and only a `CURL_SOCKET_TIMEOUT` action every 10 ms (`ManagedMultiTransfer.RunningTransferCheckInterval`) could
wake the transfer. BL-3976 cannot drop that timer until such a transfer has a socket.

## Decision

Once a dial gives a connection, `SocketActionGatedTcpDialer` calls `TransferSocketWait.TakeSocket` with the
connection's `SocketHandle`. When the socket is not yet known it becomes that handle, or, with none, the engine's
next number counting up from `TransferSocketWait.FirstEngineSocket` (`0x50000000`; the first is
`FirstEngineSocket + 1`), shared by every transfer in the process - the same scheme `ConnectOnlyConnection.FirstSocket`
(`0x60000000`) uses for connect-only connections, in a range of its own, far above any descriptor or handle the
operating system gives a process and apart from `ManagedMultiTransfer.FirstTransferSocket` (`0x40000000`).
`TransferSocketWait.SignalOn(socket, events)` signals the wait when an action names its socket and answers whether
it did, so the multi handle can route `curl_multi_socket_action` by socket.

An application cannot poll that number; libcurl gives one only for a real socket. Only the conformance harness has
connections with no socket, and its libtest runs (`Lib530Run`, `Lib582Run`) already treat every socket in their
sets as ready (ADR-0663), so they need no change to act on it.

## Consequences

- With BL-3976 wiring `ManagedMultiTransfer` to `TransferSocketWait`, every socket-action transfer has a socket to
  tell the application of, so the `FirstTransferSocket` numbers and the 10 ms timer can go.
- Real connections are unchanged: their socket is still the one `ConnectingOn` published, or their own handle.
