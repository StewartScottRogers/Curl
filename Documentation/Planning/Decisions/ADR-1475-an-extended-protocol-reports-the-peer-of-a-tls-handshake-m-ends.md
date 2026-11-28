# ADR-1475 — An extended protocol reports the peer of a TLS handshake `-m` ends

- Status: Accepted
- Date: 2026-11-28
- Decided by Claude under Stewart's delegation
- Task: BL-6033 (fitness finding EXT-097)

## Context

When `-m` or the connect limit ends the TLS handshake of an `amqps`, `rediss`, `mqtt5s`, `grpcs` or
other extended TLS transfer, Curl printed `%{remote_ip}` empty and `%{remote_port}` -1, where the
plain-TCP extended protocols print the server's address. Real curl prints `ip=:-1 nc=1` for an
`https` transfer ended the same way (curl 8.18.0 on Windows, measured, BL-6033 Notes), so curl's own
schemes must keep the empty answer; the extended protocols are judged by Curl's documentation.

## Decision

- A connect that fails, or is cancelled, with its TCP connection open carries the connection's local
  and remote end points: `ConnectResult.LocalEndPointBeforeFailure` and `RemoteEndPointBeforeFailure`
  on a returned failure, and a failed `ConnectResult` holding them under
  `ConnectResult.TcpConnectedOnCancellationKey` on the escaping cancellation (`ConnectResult.CarriedBy`).
- `ConnectionEndPointRecorder` keeps the first such pair, and puts it on a failed transfer's report that
  recorded no connection only while `ReportsEndPointsOfFailedConnects` is set, which
  `EndPointReportingProtocolHandler` sets for a scheme not in `ManagedCurlVersion.Protocols`.

## Consequences

curl's own schemes report as before. A successful transfer never reports a failed connect's peer.
