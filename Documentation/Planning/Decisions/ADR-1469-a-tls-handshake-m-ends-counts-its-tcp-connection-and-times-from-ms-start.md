# ADR-1469 — A TLS handshake `-m` ends counts its TCP connection, and times a timed-out transfer from `-m`'s start

- Status: Accepted
- Date: 2026-11-27
- Decided by Claude under Stewart's delegation
- Task: BL-6081 (fitness finding STD-231)

## Context

For an `https` or `wss` transfer whose TLS handshake is ended by `-m` or `--connect-timeout`, curl
prints `%{num_connects}` 1 and the elapsed `%{time_total}` (measured, BL-6028 Notes). Curl printed
`nc=0`, and for `wss` `t=0.000000` when the connector's limit ended the connect.

## Decision

- A connect that fails after its TCP connection opened says so: `ConnectResult.IsTcpConnectedBeforeFailure`
  on a returned failure, and the key `ConnectResult.TcpConnectedOnCancellationKey` in the `Data` of an
  `OperationCanceledException` that escapes the handshake. A failed transfer counts each such connect in
  `%{num_connects}`, whichever side counts the scheme's successful connections.
- When a transfer ends with exit 28 and its handler reported no timings, the engine times it from the
  `-m` watchdog's start (`ITransferContext.OperationStarted`), not from the handler's start, which is
  later and gave a total under `-m`'s value (1.76 s to 1.99 s under `-m 2`). A redirect hop still times
  from its own start, because the redirect follower measures the whole transfer from the first hop's.

## Consequences

A handler that times itself, as HTTP does, keeps its own times; its total under `-m` is BL-6082.
