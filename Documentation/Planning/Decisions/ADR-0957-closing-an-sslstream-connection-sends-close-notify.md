# ADR-0957: Closing an SslStream connection sends close_notify

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3984 (found by BL-3698, CGR-169)
- Decided by Claude under Stewart's delegation.

## Context

curl sends a TLS `close_notify` alert before it closes an https connection. Measured against
`openssl s_server -tls1_3 -ign_eof -msg` answering `HTTP/1.0 200 OK` with a five-byte body:
curl 8.18.0 (OpenSSL 3.5.5) in WSL and curl 8.22.0 (Schannel) on Windows both send
`TLS 1.3, Alert [length 0002], warning close_notify` after reading the body, and the server
answers with its own.

`SslStreamConnection.DisposeAsync` disposed the `SslStream` and the transport under it, and
`SslStream`'s dispose sends no alert, so a linux-x64 Curl closed the TCP connection bare and
s_server logged `unexpected eof while reading`.

## Decision

`SslStreamConnection.DisposeAsync` calls `SslStream.ShutdownAsync` first, in both builds, since
both curl builds send the alert. It sends none when TLS has already ended on the connection
(`ShutDownSendingAsync` sent the alert, or `ClearTlsAsync` cleared TLS, after which the bytes
travel in plain text) or when the server has already ended the transport. A failure to send it
(`IOException`, `InvalidOperationException`) is ignored: the transfer is over, and curl ignores
a `close_notify` it cannot send too.

The hand-built TLS client (`HandBuiltTlsConnection`) is not changed here.

## Consequences

- The server sees the same alert from Curl as from curl, on Linux, macOS and Windows.
- `SslStreamConnectionCloseNotifyTests` pins the alert, the cleared and ended cases and the
  ignored failure.
