# ADR-0109 — An HTTP/1.0 keep-alive body read to the close is reported left intact

- **Status:** Accepted; its second decision, not marking the connection reusable, is
  superseded by ADR-0112
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-471.

## Context

ADR-0050 has `HttpProtocolHandler` mark a connection reusable, and report
`Connection #N to host H:P left intact`, only when `HttpConnectionPersistence.KeepsAlive`
says the response leaves it open. A body with no Content-Length and no chunked coding runs
until the server closes, so `KeepsAlive` is false for it and the handler reported
`shutting down connection #N`.

Measured 2026-09-27 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1`,
`-s -v`, the server closing after the body:

| Response | curl's last `-v` line |
| --- | --- |
| `HTTP/1.0 200 OK`, `Connection: keep-alive`, body `hi` | `Connection #0 to host 127.0.0.1:P left intact` |
| the same with `Content-Length: 2` and `--ignore-content-length` | `Connection #0 to host 127.0.0.1:P left intact` |
| `HTTP/1.0 404`, `Connection: keep-alive`, no length, `-f` (exit 22) | `closing connection #0` |
| `HTTP/1.1 200 OK`, `Connection: keep-alive`, no length | `shutting down connection #0` |

With a second URL on the same command line, curl then prints
`Connection 0 seems to be dead` and `shutting down connection #0`, resolves from its DNS
cache and opens connection #1: it pooled the connection the server had closed and only
found out when it came to reuse it.

## Decision

- **The handler reports `left intact` for an HTTP/1.0 response kept alive by
  `Connection: keep-alive` whose body ran to the close**
  (`HttpConnectionPersistence.KeepsHttp10AliveUntilServerCloses`), when the exchange
  succeeded and delivered its body whole, as curl 8.21.0 does.
- **It does not mark that connection reusable.** The server has closed it; pooling it
  would have the next request on the same host sent into a dead socket and retried
  through the `Connection died, retrying a fresh connect` path, which is not what curl
  prints. Left unpooled, the next URL opens a fresh connection with the next number, as
  curl's does.

## Consequences

- `curl -v` on such a response ends with curl's line, byte for byte.
- With a second URL to the same host, curl's `Connection 0 seems to be dead` and
  `shutting down connection #0` lines before the fresh connect are not printed. Matching
  them needs the pool to hold the closed connection and check it before reuse, a change
  in `Curl.Networking.UnitLibrary`; filed as a follow-up task.

## Alternatives considered

- **Mark it reusable too.** Matches curl's pooling, but without a liveness check in
  `PoolingConnector` the second request fails on the closed socket and prints the
  died-and-retried lines instead, further from curl than today.
