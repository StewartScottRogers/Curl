# ADR-0933 — -Z stops waiting for multiplexing once a connection to the key showed HTTP/1

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3700 (CGR-171)
- Decided by Claude under Stewart's delegation.

## Context

Under `-Z` without `--parallel-immediate`, `PoolingConnector.WaitsForMultiplexing` (BL-717) makes a
transfer that finds a connection with its key still being opened wait until that connection holds a
session or is handed back, as curl's `CURLOPT_PIPEWAIT` does. Curl kept doing so for the whole run.
So when a host's first transfer closed its connection (`Connection: close`, or a `-f` error) and
`ParallelHostQueue` released the two transfers waiting for it, the first to open a connection made
the other wait until its response arrived: the transfers ran one after the other.

Measured on 2026-10-28 with curl 8.22.0 Schannel (Git for Windows ucrt64) against a C# loopback
server that delays each response by its `d=` query: for
`curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:P/a?d=600&close=1" "http://127.0.0.1:P/b?d=100" "http://127.0.0.1:P/c?d=300"`
curl opens two connections in the same millisecond once `a` ends and runs `b` and `c` side by side,
printing `0 0 200`, `1 0 200`, `2 0 200` (with `-f` and `s=500` on `a`: `0 22 500` first, exit 22).
curl decides at connection match time whether a transfer may wait for multiplexing; once the host's
connection showed HTTP/1.1 nothing waits for a pending connection there any more.

## Decision

1. A connection's first session that carries one transfer at a time (`ConcurrentTransferLimit`
   `null`, the HTTP/1.x `Http1VersionSeen`) records its `ConnectionPoolKey` in the run's
   `ConnectionCache.KeysKnownNotToMultiplex`; `PoolingConnector` never waits for a connection being
   opened to such a key. Keys whose connections multiplex, or have not shown either yet, wait as before.
2. `ParallelHostQueue` is unchanged: it still releases the waiters in ADR-0768's order and starts
   them one after another; they now open their connections side by side. curl 8.22.0 sent `/b`'s
   request first in this measurement and Curl sends `/c`'s first (ADR-0768's pending-last rule); the
   two requests go out within a few milliseconds of each other, and stdout, exit code and request
   bytes are the same.

## Consequences

- CGR-171's repro prints curl's lines in curl's order, in about curl's time.
- The rule is per key, so a different port, scheme or proxy on the same host still waits until its
  own first connection shows what it speaks.
