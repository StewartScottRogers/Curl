# ADR-1462 — A cancelled connect carries the resolving fact and a connection number taker

- **Status:** Accepted
- **Date:** 2026-11-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6074.

## Context

On a redirect hop `-m` runs out before the connector's own limit, so the connect leaves the
connector as an `OperationCanceledException` and the HTTP handler reports it (ADR-1460). The
handler knew neither the connection's number nor that the name was still being resolved: it
printed `closing connection #0` and always `Connection timed out`.

Measured with `Record-CurlExchange.ps1` on Windows (curl 8.22.0, a debug build):
`-svSL --trace-time -m 2 --doh-url http://127.0.0.1:18474/dns` following a 302 answered 1.5 s
late to `http://slow.example.test:1/`, whose DoH queries get no answer in time, prints
`* Resolving timed out after 426 milliseconds`, `* closing connection #1` and
`curl: (28) Resolving timed out after 426 milliseconds`. N counts from the start of the hop.

## Decision

The connectors put two facts into the cancellation's `Exception.Data`, under
`ConnectCancellationKeys` in `Curl.Protocol.Abstractions`:

1. `TcpConnector` marks a cancellation that arrived while a name was being resolved.
2. `PoolingConnector`, which numbers connections, adds a function that takes the next number.
   The handler calls it only when it reports the connect as failed, so a connect that lost a
   race takes no number. A QUIC connect gets none, as it is not numbered when it fails
   (ADR-0338).

The connector still throws: only the caller knows why it cancelled.

## Consequences

- A connector that does not unwind within the handler's 100 ms grace leaves neither fact, so
  the handler says `Connection timed out` and `closing connection #0`.
- An `--http3-only` hop that `-m` ends is still numbered `0`.
