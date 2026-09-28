# ADR-0112 — The pool reports a connection whose close was read as dead

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-477.
Amends ADR-0109.

## Context

ADR-0109 has `HttpProtocolHandler` report `left intact` for an HTTP/1.0 keep-alive response
whose body ran to the server's close, but not mark the connection reusable, because
`PoolingConnector` had no way to tell the connection was closed. curl 8.21.0 pools it and,
when a second URL would reuse it, prints `Connection 0 seems to be dead` and
`shutting down connection #0` before opening connection #1.

Measured 2026-09-27 with curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`, `-s -v`,
two URLs to the same host, the server closing each connection after its response:

| Response | What curl prints for the second URL |
| --- | --- |
| `HTTP/1.0 200 OK`, `Connection: keep-alive`, body `hi` (no length) | `Connection 0 seems to be dead`, `shutting down connection #0`, `Hostname 127.0.0.1 was found in DNS cache`, `Trying`, connection #1 |
| `HTTP/1.1 200 OK`, `Content-Length: 2` | `Reusing existing http: connection`, `Recv failure: Connection was reset`, `Connection died, retrying a fresh connect (retry count: 1)`, ... connection #1 |
| `HTTP/1.1 200 OK`, `Connection: close`, `Content-Length: 2` | `shutting down connection #0` after the first; `Hostname 127.0.0.1 was found in DNS cache` and a fresh connect |

So curl calls a pooled connection dead only when it already read the server's close while
reading the body. A connection the server closed while it sat idle, with nothing left to
read, is reused and dies on the request, which the handler already matches (BL-336).

## Decision

- **`PooledConnection` notes a read that found the end of the stream**: a read into a
  non-empty buffer that returns zero sets `PoolEntry.HasReadPeerClose`.
- **`PoolingConnector` never hands out such a connection.** When it takes one from the pool
  it reports `Connection N seems to be dead` and `shutting down connection #N` on the new
  transfer's events, closes it, and tries the next idle connection with the key; with none,
  the inner connector opens a fresh one with the next number.
- **The handler marks a connection reusable whenever it reports it `left intact`**, the
  HTTP/1.0 keep-alive body read to the close included, so the pool holds it as curl does.
- No socket-level liveness probe (a zero-byte peek). It would call the idle-closed
  HTTP/1.1 connection above dead too, which curl does not.

## Consequences

- Two URLs against an HTTP/1.0 keep-alive server that closes print curl's two dead-connection
  lines before the fresh connect, in curl's order.
- `Hostname H was found in DNS cache`, which curl prints before any fresh connect to a host
  it already resolved, is still not printed; that is a gap in the TCP connector for every
  second connection, not in the pool, and is tracked as its own task.

## Alternatives considered

- **Probe the socket before reuse.** Needs a new member on `IConnection` in
  `Curl.Protocol.Abstractions.UnitLibrary`, and reports connections dead that curl reuses,
  as measured above.
- **Keep the connection out of the pool (ADR-0109).** Loses curl's two lines.
