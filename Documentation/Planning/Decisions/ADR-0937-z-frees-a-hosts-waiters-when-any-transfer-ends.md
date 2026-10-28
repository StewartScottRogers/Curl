# ADR-0937 — -Z frees a host's waiting transfers when any transfer ends

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3701 (CGR-172)
- Decided by Claude under Stewart's delegation.

## Context

Under `-Z` without `--parallel-immediate`, `ParallelHostQueue` holds a host's later `http://` transfers
until the host's first transfer ends (BL-520, ADR-0768). It freed them only when a transfer to that same
`host:port` ended. curl 8.22.0 keeps such a transfer pending and retries its pending transfers whenever
any transfer is done: one to another host, or one whose connect was refused. By then the host's first
connection is open and, over cleartext HTTP/1.1, cannot multiplex, so the retried transfer opens a
connection of its own beside it.

Measured on 2026-10-28 with curl 8.21.0 Schannel (`C:\windows\system32\curl.exe`) against a C# loopback
server that delays each response by its `d=` query (ports 22000 and 22001):
`curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600" "http://127.0.0.1:22001/q?d=100" "http://127.0.0.1:22000/c?d=300"`
prints `1 0 200`, `2 0 200`, `0 0 200`; the server sees `/c` on a second connection about 115 ms after
`/a`, once `/q` has ended. The gap report measured the same on curl 8.22.0 and 8.18.0 on Linux.

Curl's connection pool also held the freed transfer: `PoolingConnector.WaitsForMultiplexing` treated every
plain TCP connection straight to the origin as one that may multiplex (HTTP/2 with prior knowledge) and
made the freed transfer wait for its session, which an HTTP/1.1 connection only holds once its response
arrives.

## Decision

1. `ParallelHostQueue.Leave` frees, beside the ended transfer's own host's waiters, every host's
   transfers still waiting for that host's first to end, in the order they waited, and counts those hosts'
   first as ended. A transfer with no host (`file://`) ending frees them too, as curl's retry follows any
   transfer done. `--parallel-max-host` still limits how many start.
2. `PoolingConnector.PlainTcpMayMultiplex` (default `true`) says whether a plain TCP connection straight
   to the origin may multiplex. `EngineComposition.CreatePoolingConnector` sets it only for `--http2` and
   `--http2-prior-knowledge`, so in an HTTP/1.x group a transfer that finds such a connection busy opens
   its own, as curl does. TLS connections keep deciding by ALPN as before.

## Consequences

- CGR-172's first repro prints curl's lines in curl's order; the freed transfer starts later than in curl
  by Curl's own connect overhead, so when `/c` ends within a few tens of milliseconds of `/a` the two
  lines can still swap.
- In the refused-connect repro Curl prints the gap report's expected `1 7 000`, `2 0 200`, `0 0 200`.
  The Windows curl 8.21.0 measured here takes about 2 seconds to report the refused loopback connect,
  so it prints `0 0 200`, `2 0 200`, `1 7 000`; that connect timing is not this decision's.
- curl retries a pending transfer while its host's first connection is still being opened as well and
  keeps it pending then; Curl frees it at once, so in that rare case it opens a second connection
  sooner than curl.
