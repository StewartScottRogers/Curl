# ADR-1455 — A transfer is numbered before its `--doh-url` connections

- **Status:** Accepted
- **Date:** 2026-11-26
- **Amends:** ADR-1441, whose last bullet recorded the console numbering a transfer after its DoH look-up

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6058.

## Context

Measured 2026-11-26 with curl 8.18.0 (Windows nghttp2 build and Ubuntu) over three URLs against an
HTTP/1.1 DoH server: the transfers are `#0`, `#2`, `#4` and the DoH connections `#1` and `#3`. curl
numbers a connection before it resolves its host. Curl counted the two separately: `%{conn_id}` came
out 1, 3, 4, `closing connection #N` 0, 1, 2 and the DoH connections `#1`, `#2`.

Curl has two counts: the console's `%{conn_id}` (`TransferPerformer`) and the pool's connection
number (`ConnectionCache`, ADR-0109), which `-v` prints and which was taken once a connect ended.

## Decision

- Each DoH connection takes a number from both counts, and the connection it resolves for takes its
  own first, when it holds none:
  - `TransferPerformer.TakeSubTransferConnectionId` has the runner give the running transfer its
    `%{conn_id}` (`NumberRunningTransfer`) before taking the DoH connection's.
  - `ConnectionCache.NumberSubTransferConnection` reserves a number for the connection a
    `PoolingConnector` is opening in the same flow before taking the next; the pool gives that
    reserved number to the connection, or to its failed connect, when the open ends.
- `-v` gives a DoH connection the pool's number.
- A transfer that opens no DoH connection is numbered where it was before.
- The dialing runner the conformance harness uses numbers its DoH connections the same way.

## Consequences

A QUIC connect that fails after a DoH look-up leaves its reserved number unused, as the pool does not
number a failed QUIC attempt. A library handle's transfers are not covered: its DoH connections take
no number from the engine.
