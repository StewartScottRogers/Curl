# ADR-0338 — HTTP/3 sessions are pooled, and `-Z` transfers share one up to the server's MAX_STREAMS

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-735.
It builds on ADR-0050 (the connection pool), BL-717 (HTTP/2 multiplexing in the pool) and
ADR-0144/ADR-0172 (HTTP/3 over `IMultiplexedConnection`).

## Context

curl.se's 8.18.0 ngtcp2 build, measured with `-Z --http3-only -v -w '%{num_connects}\n'` and
three URLs on one origin (BL-735 Notes), opens one QUIC connection, sends the three requests on
streams 0, 4 and 8, prints `Multiplexed connection found` and `Reusing existing https:
connection with host H` for the second and third, reports `num_connects` 1, 0 and 0, and leaves
the connection intact once. Curl opened a new QUIC connection for every transfer: the pool
forwarded `ConnectMultiplexedAsync` untouched, and the HTTP handler never marked an HTTP/3
connection reusable.

The pool cannot build the HTTP/3 session itself (`Curl.Networking` must not reference the HTTP
library), and an HTTP/3 connection cannot be shared below the session: each session opens the
client's control and QPACK streams, which RFC 9114 allows once per connection.

## Decision

1. `IConnector` gains `ConnectMultiplexedSessionAsync(target, openSession, token)`: the caller
   passes the function that builds its session over a new QUIC connection, and gets the session
   back as a `ConnectResult`. The default implementation opens a new QUIC connection every time
   (`MultiplexedConnectResult.ToConnectResult`), so every existing connector is unchanged.
2. `PoolingConnector` overrides it with the same share / idle / open path as `ConnectAsync`,
   under a `ConnectionPoolKey` with `IsQuic` set, so a TCP connection to the same origin is never
   handed to an HTTP/3 transfer or the other way round. The session is known once the handshake
   ends, so transfers waiting for multiplexing (`CURLOPT_PIPEWAIT`) wait only for the handshake.
3. `Http3Session` is an `IConnectionSession`. Its `ConcurrentTransferLimit` is the server's
   current `MAX_STREAMS` for client bidirectional streams (`IMultiplexedConnection.BidirectionalStreamLimit`,
   which `QuicConnection` reads from its stream set), unlimited when unknown, and 0 after a GOAWAY
   or a refused stream. Past it the pool reports `MAX_CONCURRENT_STREAMS reached, skip (N)` and
   opens another connection, as it does for HTTP/2. QUIC's `MAX_STREAMS` is cumulative, not
   concurrent; using it as the concurrent limit is the simplest reading that never shares a
   connection that could not open the stream, and a server raises it as streams close.
4. The handler marks an HTTP/3 connection reusable while its session takes new requests, so the
   last transfer reports `left intact` and the pool closes it with `H3_NO_ERROR` at the end of
   the run. `Http3Session` opens request streams one transfer at a time, so concurrent
   transfers never open the control streams twice.
5. A failed QUIC connect is not numbered by the pool: `--http3` races it against a TCP connect,
   which curl counts as the same connection, so the TCP connect that wins keeps `#0`.

## Consequences

- `-Z --http3` and `--http3-only` transfers to one origin share one QUIC connection; sequential
  HTTP/3 transfers reuse it too, as curl does.
- curl's `Connection #0 is not open enough, cannot reuse` / `Found pending candidate for reuse
  and CURLOPT_PIPEWAIT is set` / `Waiting on connection to negotiate possible multiplexing.`
  lines are not written, for HTTP/3 as for HTTP/2 (BL-717); they stay a gap of the pool.

## Alternatives considered

- **Pool the bare `IMultiplexedConnection`** and let each transfer build its own session: two
  sessions would open two control streams on one connection, an `H3_STREAM_CREATION_ERROR`.
- **Keep the session cache in the HTTP handler:** the pool already owns numbering, sharing,
  `MAX_CONCURRENT_STREAMS`, idle limits and the run's cache across option groups; a second cache
  would duplicate all of it and disagree with `%{num_connects}`.
