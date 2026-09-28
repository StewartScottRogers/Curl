# ADR-0109 — A failed connect takes the next connection number

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-469.

## Context

ADR-0105 has `HttpProtocolHandler` report `closing connection #N` after a failed connect,
with `N` from `ConnectResult.ConnectionNumber`. `ConnectResult.Failed` and `Refused` took no
number, and `TcpConnector` and `PoolingConnector` numbered only the connections that opened,
so every failed connect said `#0`.

Measured 2026-09-27 with curl 8.21.0 (Windows, Schannel),
`curl -sv http://nohost.invalid/ http://127.0.0.1:1/`:

```
* Could not resolve host: nohost.invalid
* closing connection #0
* connect to 127.0.0.1 port 1 from 0.0.0.0 port 63841 failed: Connection refused
* Failed to connect to 127.0.0.1:1 after 2032 ms: Could not connect to server
* closing connection #1
```

curl creates the connection, and numbers it, before it resolves the host, so a resolve
failure takes a number as a refused connect does. An option curl rejects at setup
(`--resolve` or `--connect-to` that does not parse, exit 49) fails before any connection
exists.

## Decision

- **`ConnectResult.Failed(exitCode, errorMessage, timings, connectionNumber = 0)` and
  `ConnectResult.Refused(errorMessage, timings, connectionNumber = 0)`** carry the number on
  `ConnectionNumber`. The two-argument overloads stay `0`.
- **`TcpConnector` numbers every connect that gets past its options**: a success when it
  opens, as before, and a failure (resolve, dial, tunnel, TLS) when it fails, from the same
  counter. The exit 49 of an option that does not parse takes no number.
- **`PoolingConnector` numbers the inner connector's failures from its own counter**, as it
  already renumbers the inner connector's successes; the failure keeps its exit code,
  message, timings and refused mark. A reused connection takes no new number.
- The two connectors rebuild a failure through one internal helper,
  `NumberedConnectFailure.Of`, so a refused failure stays refused.

## Consequences

- A redirect to a refused port after connection `#0` reports `closing connection #1` once
  `HttpProtocolHandler` passes `ConnectResult.ConnectionNumber` on, as ADR-0105 has it do.
- A failure is numbered when it completes and a success when it is ready, so with
  concurrent transfers (`--parallel`) the order of numbers follows completion rather than
  curl's creation order. A single sequence of transfers numbers as curl does.
- A failure is no longer returned as the same `ConnectResult` instance the inner connector
  or TLS provider built; tests compare its exit code and message instead.

## Alternatives considered

- **Take the number at the start of `ConnectAsync`**, as curl does at creation: the number
  would have to be threaded through every path to `Opened`, for a difference only
  `--parallel` can show.
- **A `WithConnectionNumber` copy method on `ConnectResult`**: a second way to build a
  result, where the task asked for the number on the factories that build failures.
