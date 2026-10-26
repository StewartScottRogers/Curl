# ADR-0816: An FTP upload's data connection is shut down and drained before it closes

- **Status:** Accepted
- **Date:** 2026-10-26
- **Decided by Claude under Stewart's delegation** (BL-3535, third-party gap report CGR-006)

## Context

After the last byte of an upload, `FtpSession.CopyUploadAsync` disposed the data connection
at once. Over FTPS with TLS 1.3 the server sends a session ticket after the handshake, which
the client never reads during an upload. Windows resets a socket closed with unread bytes,
and the reset makes the server's stack throw away whatever of the upload it had not read
yet: the server saw `An existing connection was forcibly closed by the remote host`, a strict
server answered `426` (exit 18), and a lenient one could store an empty file while Curl
exited 0. Measured on Windows with a TLS 1.3 FTPS server (passive explicit, active implicit,
active explicit): curl 8.22.0 Schannel exits 0 and the server reads the 8 bytes and a clean
end; Curl before the change exited 18 after `426`.

curl 8.22.0 shuts a connection down before closing it: it sends `close_notify`, ends the send
side and waits up to its shutdown timeout (`DEFAULT_SHUTDOWN_TIMEOUT_MS`, two seconds) for the
peer, reading what it still sends.

## Decision

`FtpSession.CloseUploadDataConnectionAsync` ends an upload's data connection as curl does:
`ShutDownSendingAsync` (`close_notify`, then FIN), then reads until the server closes its
side, a read fails, or two seconds pass on the transfer's `TimeProvider`, then disposes it.
The download and listing close path (`CloseDataConnectionAsync`) is unchanged: there the
client has already read to the end of the data.

## Consequences

- The server reads the whole upload and a clean end of stream, and answers `226`, as with
  real curl; the measured repros now match curl's exit code, output and stored file.
- A server that keeps the data connection open after the client's FIN delays the end reply
  by at most two seconds, as it does with curl.
