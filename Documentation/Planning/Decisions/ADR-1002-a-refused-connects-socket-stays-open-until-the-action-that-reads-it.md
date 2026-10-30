# ADR-1002: A refused connect's socket stays open until the action that reads it

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4086)

## Context

CGR-034 `C_65`: a transfer to a refused port driven through `curl_multi_socket_action`. libcurl 8.22.0
(`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) gives `T0 add S1=2 T-1 kick S1=4`, the socket a real one
the application polls until its REMOVE callback. After BL-4075's connect grace the managed connect, refused
within the grace on Windows loopback, closed its socket before the socket callback was told of it, and asked
for a timer of 0: `T0 add S1=2 T0 kick S1=4 T-1`, `S1:NOT-A-SOCKET`.

## Decision

`ITcpDialer.HoldingFailedSocketsUntil` gives a dialer that awaits a hook before it closes a TCP socket whose
dial failed (`TcpDialer.CloseFailedSocketAsync`); the default holds nothing. A socket-action transfer's
`SocketActionGatedTcpDialer` passes `TransferSocketWait.HoldFailedConnectAsync`, which holds the failure,
the transfer still waiting writable on its socket, until the multi handle signals that socket `Out` or
`Error`, gating stops, or the dial is cancelled (its connect deadline or the transfer's end). A connect the
application signalled already is not held. So the action is told of a real socket with no deadline (timer
-1), the application's poll on it reports the failure, and that action fails the transfer and removes the
socket, as libcurl's does. Outside socket-action mode nothing changes.

## Consequences

The probe gives `PORT C_65: T0 add S1=2 T-1 kick S1=4 | results 7 | real: S1:real`; `C_20`, `C_21`, `C_74`
and `C_75` still equal REF. A failure before the connect itself (a local bind) is held the same way, as the
socket was already told of. Pinned by `Cgr034RefusedConnectTests` and `TcpDialerSocketCreatedTests`.
