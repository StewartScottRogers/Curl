# ADR-0221 — A TLS read that ends without close_notify fails with curl's exit 56 text on both TLS paths

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-819.
Builds on ADR-0157 decision 1 (the hand-built streams return 0 at a bare end and say so
with `CloseNotifyReceived`) and ADR-0162 decision 7 (both TLS paths returned 0 for both
endings, leaving exit 56 as follow-up work).

## Context

When the server of an `https://` transfer closes its TCP connection without sending TLS
`close_notify`, curl fails any read that finds that end with exit 56. Curl's two TLS
paths both returned 0 for it, so a read-until-close body ended "successfully" with exit 0,
and a short body reported the generic `Recv failure` text. `SslStream` returns 0 at a
record boundary whether the peer sent `close_notify` or not, and has no property that
says which.

### Measured 2026-09-29

`Record-CurlExchange.ps1 -Tls` (this task added `-TlsCloseNotify`, which sends
`close_notify` through the server's `SslStream.ShutdownAsync` before closing), curl run as
`curl -sS -k https://<host>:48219/`. Windows: the reference build, curl 8.21.0 (mingw,
Schannel). Linux: curl 8.18.0 (Ubuntu under WSL, OpenSSL 3.5.5), with
`-Curl wsl.exe -ListenAddress <Windows host on the WSL network>`.

| Response, ending | Schannel build | OpenSSL build |
| --- | --- | --- |
| `Connection: close`, body `hello`, no `close_notify` | exit 56, stdout `hello`, `curl: (56) schannel: server closed abruptly (missing close_notify)` | exit 56, stdout `hello`, `curl: (56) OpenSSL SSL_read: OpenSSL/3.5.5: error:0A000126:SSL routines::unexpected eof while reading, errno 0` |
| `Connection: close`, body `hello`, then `close_notify` | exit 0, stdout `hello` | exit 0, stdout `hello` |
| `Content-Length: 10`, body `hello`, no `close_notify` | exit 56, same text | exit 56, same text |
| `Content-Length: 5`, body `hello`, no `close_notify` | exit 0 (curl never reads past the body) | exit 0 |

## Decision

1. `Curl.Protocol.Abstractions` gains `MissingCloseNotifyException`, an `IOException`
   carrying the build's text. A secure `IConnection` throws it from a read into a
   non-empty buffer that finds the connection ended without `close_notify`; a read
   after `close_notify` still returns 0.
2. The text is the platform's build, chosen by the provider's `matchesSchannelBuild`
   (`TlsFailureMessages.MissingCloseNotify`): Schannel's line as measured, and the
   OpenSSL line with the version of the OpenSSL reference build ADR-0085 already names,
   `curlimages/curl:8.21.0`'s OpenSSL 3.5.7, rather than the 3.5.5 the WSL build printed.
3. `HandBuiltTlsConnection` asks the hand-built stream's `CloseNotifyReceived`
   (ADR-0157) when a read returns 0.
4. `SslStreamConnection` tells the endings apart from the transport underneath:
   `ConnectionStream.TransportEnded` records that a read of the plaintext connection
   returned 0. `SslStream` reads the transport to its end only at a bare end; after
   `close_notify` it returns 0 without reading further. So a 0 from `SslStream` with the
   transport ended, or an `IOException` from `SslStream` with the transport ended (the
   end fell inside a record), is the missing `close_notify`. An `IOException` with the
   transport not ended (a reset) is left as it was, so exit 56 keeps its `Recv failure`
   text there.
5. `PooledConnection` marks the connection dead when a read throws the exception, as it
   does for a read that returns 0.
6. The HTTP handler's `HttpTransferMessages.ReceiveFailure` reports the exception's own
   message as the exit 56 text, before the reset and generic cases; the bytes already
   written stay written, as curl's do.

## Consequences

- A read-until-close body over TLS now ends as curl's does, with exit 56 unless the
  server sent `close_notify`; a server that closes without it after a complete
  `Content-Length` or chunked body is unaffected, since no read runs into the end.
- Any protocol handler that catches `IOException` sees the new exception; only the HTTP
  handler maps its text so far. Other handlers report their existing exit 56 text until
  they map it too.
- Decision 4 relies on `SslStream` not reading its inner stream after `close_notify`,
  which `TlsConnectionCloseNotifyTests` pins on each run.

## Alternatives considered

- **Leave the `SslStream` path returning 0 and fail only the hand-built path.** Two TLS
  paths would answer the same server differently; ADR-0162 requires they agree.
- **Parse TLS records under `SslStream` to spot the `close_notify` alert.** After the
  handshake alerts are encrypted, so the record layer cannot see which alert it is.
- **Map the failure in each handler from a generic `IOException`.** The handler cannot
  tell a missing `close_notify` from any other read failure, and the text belongs to the
  TLS build, which only the provider knows.
- **Print the OpenSSL version of whichever build was measured (3.5.5).** Every other
  OpenSSL text in the solution pins the reference build's version; one text with another
  version would contradict them.
