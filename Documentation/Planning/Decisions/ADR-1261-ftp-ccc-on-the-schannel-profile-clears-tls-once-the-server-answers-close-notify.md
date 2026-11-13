# ADR-1261 — FTP's `CCC` on the Schannel profile clears TLS once the server answers `close_notify`

- **Status:** Accepted
- **Date:** 2026-11-12
- **Supersedes:** [ADR-1241](ADR-1241-ftp-ccc-on-the-schannel-profile-stays-exit-81-as-measured-on-curl-8-22-0.md)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4910.

## Context

ADR-1241 kept exit 81 for `--ftp-ssl-ccc` on the Schannel profile because curl 8.22.0 Schannel
(System32 and Git for Windows) failed with exit 81 against a .NET `SslStream` stand-in, whichever
side sent `close_notify` first. The fitness review of 2026-11-12 re-reported STD-007 with its
server stack: an OpenSSL server (Python's `ssl`, `sock.unwrap()` on `CCC`, a bidirectional
`close_notify` exchange). Against it curl 8.22.0 Schannel exits 0 in passive and active mode, for
a download, an upload and implicit FTPS, and the server reads `PWD` in plain text after
`200 CCC ok`; `-v` shows `schannel: shutting down SSL/TLS connection with 127.0.0.1 port N` and
`schannel: server close notification received (close_notify)`. So curl's result depends on how
the server answers its `close_notify`: both measurements hold.

## Decision

On the Schannel profile `SslStreamConnection.ClearTlsAsync` and `HandBuiltTlsConnection.ClearTlsAsync`
send `close_notify` in either `--ftp-ssl-ccc-mode`, as curl's `schannel_shutdown` does, then read
one record: the server's `close_notify` (the read returns 0 with the transport still open) clears
TLS and the transfer goes on in plain text; data, a bare end or a reset in its place is exit 81
with no `QUIT`, as before. `SslStreamConnection` writes the two `schannel:` lines above on the
handshake's events (the host the handshake verified, the server's port). The OpenSSL profile is
unchanged (ADR-0280).

The difference between the OpenSSL server and the .NET stand-in that ADR-1241 measured was not
isolated in this run (no OpenSSL server could be run here without Python); Curl clears whenever
the server's `close_notify` arrives, which matches every OpenSSL-backed server and the review's
four commands. Against a .NET `SslStream` server that answers `close_notify`, Curl may exit 0
where curl 8.22.0 measured 81; that is accepted as the smaller divergence, since a real FTPS
server is far likelier to be OpenSSL-backed.

## Consequences

- `--ftp-ssl-ccc` works on Windows against OpenSSL-backed servers, as the platform's curl does.
- The hand-built TLS path (TLS 1.0/1.1 ceilings and the other ADR-0140 rows) clears the same way
  but writes no `schannel:` lines, since its connection does not know the host name.
