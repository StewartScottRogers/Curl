# ADR-0963: A SOCKS4 host name left with no IPv4 address is unresolved, exit 6

Status: Accepted. Decided by Claude under Stewart's delegation (BL-3739, CGR-210).

## Context

`socks4://` resolves the target locally and sends an IPv4 address. With
`--resolve ex.test:80:[::1]` Curl exited 97 `SOCKS4 connection to ::1 not supported`.
Measured with curl 8.22.0 (Schannel, Windows): that command exits 6
`Could not resolve host: ex.test`, while the IPv6 literal `http://[::1]/x` stays exit 97
`SOCKS4 connection to ::1 not supported`.

## Decision

`Socks4Handshake` reports a host name with no IPv4 address as `Could not resolve host`
(exit 6); only an IPv6 address literal keeps the exit 97 "not supported" text.
