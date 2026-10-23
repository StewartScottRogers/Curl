# ADR-0770 — A data connection ends its send side before it closes

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3179
- Decided by Claude under Stewart's delegation.

## Context

BL-3161 moved FTP's data-connection close ahead of the end-of-transfer reply, as curl 8.22.0's
`ftp_done` closes it. `IConnection` had only `DisposeAsync`, so a TLS data connection closed
without `close_notify`, and a socket closed with bytes still unread (a server's `close_notify`
after the last data byte) made the operating system send RST. Servers such as test.rebex.net
answer such a close with `426 ... unable to close data connection gracefully`.

## Decision

`IConnection.ShutDownSendingAsync` is a default-implemented member (doing nothing, as a test fake
needs) that ends the send side gracefully:

- `SslStreamConnection` and `HandBuiltTlsConnection` send `close_notify`, then call it on the
  plaintext connection under them.
- `StreamConnection`, over a socket's `NetworkStream`, discards the bytes already received but
  unread (`Socket.Available`, so it never blocks), then calls `Socket.Shutdown(SocketShutdown.Send)`
  (FIN); a `SocketException` there means the peer is gone and is ignored. Over any other stream it
  does nothing.
- `TcpIoTraceConnection` passes it through. Other wrappers (`PooledConnection`,
  `EarlyDataTlsConnection`, `Http2ProxyTunnelConnection`, the engine's socket-callback connection)
  keep the default for now: a data connection is never pooled, and the default only means the old
  close.
- `FtpSession` calls it on the data connection, then disposes it, before it reads the
  end-of-transfer reply; an `IOException` from the shutdown is ignored, as curl ignores a failed
  `SSL_shutdown`.

This is the order curl's OpenSSL build closes a TLS data connection in (`SSL_shutdown` sends
`close_notify`, then the socket closes). The order was taken from the task and from curl's
source; it was not measured with `Record-CurlExchange.ps1 -Ftp -Tls` in this run, whose
recorder does not show the TCP shutdown. No output text changes.

## Consequences

A plain or TLS FTP data connection ends with `close_notify` and FIN rather than a reset over
unread bytes. Bytes arriving after the discard and before the close can still draw a reset;
waiting for the server's FIN would cost a round trip curl does not spend.
