# ADR-1459: Under -k an SslStream chain build that throws is redialled on the hand-built client

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6066)

## Context

ADR-1452: on Linux `SslStream`'s own chain build throws for OpenSSL error 1 inside the handshake, so
under `-k` (or `--proxy-insecure`) `SslStreamTlsProvider` could only end the connect with exit 35,
where OpenSSL curl goes on with verify result 1. The hand-built client builds the chain through
`CertificateChainBuild` (ADR-1446) and goes on, but needs a connection of its own.

## Decision

- In the OpenSSL build, under `-k` or `--proxy-insecure`, `SslStreamTlsProvider` marks that failed
  result as asking for a redial and remembers the host (`ITlsProviderWithHandBuiltRedial`). It
  reports the verify result but no failed handshake.
- `TcpConnector` unwinds the connect, dials again and runs the handshake with that host on a
  `HandBuiltTlsProvider` built from the same settings. This covers every route that handshakes
  through the connector: direct, forward proxy, tunnel, SOCKS, Unix socket, and the CONNECT-UDP
  tunnel's HTTPS proxy.
- The redialled TCP connect runs in what is left of the first one's `--connect-timeout`, takes one
  connection number between the two dials, and later connects to the host in the same run use the
  hand-built client from the first dial. The CONNECT-UDP route's second attempt has a limit of its
  own.
- A caller that handshakes without the connector (`AUTH TLS`, `STARTTLS`) has no way to dial again
  and keeps exit 35.
- The redialled handshake offers no cached session and no ECH configuration, as `SslStream`'s route
  has neither option.

The whole connect is repeated, not only the dial, so a tunnel is opened again the same way it was
opened the first time. The lines the first attempt reported are not shown again (ADR-1461,
BL-6073).

Rejected: running every `-k` handshake on the hand-built client, as ADR-1452 rejects.

## Consequences

- `-k` against such a server completes the transfer with `%{ssl_verify_result}` 1.
- The server sees two connections, the first abandoned during its handshake.
- The Schannel build is unchanged.
