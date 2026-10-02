# ADR-0346 — A TFTP transfer's connection is numbered with the run's other connections

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-969.

## Context

curl numbers every connection it creates from `#0` across the whole run, a failed one
included (ADR-0109). TCP connections are numbered by the run's `ConnectionCache` through
`PoolingConnector`, but a TFTP transfer opens a UDP channel through `IDatagramConnector`,
whose `DatagramOpenResult` carried no number, so `TftpProtocolHandler` always printed
`shutting down connection #0`. curl 8.21.0 Schannel, measured with
`Record-CurlExchange.ps1 -Tftp` (BL-969 Notes), prints `#0` then `#1` for
`-v tftp://h/a tftp://h/b`, and `#1` for a TFTP URL after an HTTP one whose connection `#0`
was left intact.

## Decision

`DatagramOpenResult` carries a `ConnectionNumber` (`0` by default, set by
`WithConnectionNumber`). `PoolingConnector.NumberingDatagrams` wraps a datagram connector so
each open takes the next number from that pool's `ConnectionCache`, a failed open's included,
as `PoolingConnector` numbers a failed TCP connect. `CurlComposition.CreateProtocolHandlers`
gives the TFTP handler that wrapper when its connector is a `PoolingConnector`, and the
handler prints the number its channel was opened with.

Only TFTP's channel is numbered: the same `UdpDatagramConnector` also carries Kerberos KDC
requests, which curl makes through GSS-API, outside its connection cache, so they take no
number.

## Consequences

- `CurlCommandRunnerTftpConnectionNumberTests` pins both measured cases end to end, and
  `TftpTransferEventsTests` pins `#N` from the opened channel's number.
- A composition over a connector that is not a `PoolingConnector` (some tests) leaves TFTP
  at `#0`, as before.

## Alternatives considered

- **Number inside `UdpDatagramConnector`.** Lost: it would also number the Kerberos KDC's
  datagrams, shifting every later connection's number away from curl's.
- **A counter of the datagram connector's own.** Lost: an HTTP connection before the TFTP
  URL would not move the TFTP number to `#1`, which curl does.
