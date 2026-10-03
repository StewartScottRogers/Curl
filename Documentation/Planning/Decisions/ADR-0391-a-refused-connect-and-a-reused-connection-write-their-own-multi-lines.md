# ADR-0391 — A refused connect and a reused connection write their own `[MULTI]` lines

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1212, following ADR-0382 and BL-1188. curl 8.21.0 (Schannel), measured on 2026-10-02 with
`Record-CurlExchange.ps1` under `-s -v --trace-config multi` (BL-1212 Notes):

- `http://127.0.0.1:1/` (exit 7): after `Trying`, the poll lines `pollset[fd=N OUT]`, `multi_wait` and
  `cf_setup_connect` come once per second of Windows' connect retries (twice in a 2 s refusal), then
  `Curl_multi_will_close fd=N`, the `connect to ... failed` and `Failed to connect to` lines, five
  `[CONNECTING]` lines (`failed to connect`, `connect failed -> 7`, `multi_done: status: 7 prem: 1`,
  `multi_done_locked`, `multi_done, terminating conn #N to <host:port>, ...`), `closing connection #N`,
  then `-> [COMPLETED]`, three `[COMPLETED] [PGRS-*] added` lines, `-> [MSGSENT]`, `removed from multi`.
- The second of two URLs on one kept-alive connection: `transfer credentials: -`, the `Reusing
  existing` line, `[PGRS-POSTQUEUE] set`, `-> [CONNECTING]`, `[CONNECTING] -> [PROTOCONNECT]`,
  `[PROTOCONNECT] -> [DO]`, `xfer_setup`, then the request and the same lines as a new connection.
  No `[CPOOL]`, `Curl_conn_setup`, connect group, `reduced to` or `using HTTP/` line.

## Decision

1. `MultiStateTraceEvents.ReportConnectionReused` writes the reused connection's lines around the event
   and goes on from the `Request completely sent off` group.
2. Before each `connect to ... failed` line it writes the connect's poll lines (once, if not yet
   written) and `Curl_multi_will_close fd=<the poll lines' descriptor>`. An address that fails before
   one that connects writes its close line too, as curl closes that socket.
3. The `Failed to connect to <host:port> after` line turns it to the failed connect's own two groups:
   the five `[CONNECTING]` lines after that line, with the connection number the `[CPOOL]` line took and
   the line's host and port, and the `[COMPLETED]` lines after `closing connection #`. Status 7 is
   written, as the `Failed to connect` line belongs to exit 7 alone.
4. The poll lines are written once, not once per second of the connect: the count follows the
   platform's SYN retries (one on a Linux refusal, two on Windows), which Curl does not see, and a
   test pinning a timing-dependent count would be flaky.

## Consequences

- A refused connect's and a reused connection's `[MULTI]` lines match curl's apart from repeated poll
  rounds during a slow refusal.
- A connect that times out (exit 28) writes no failed-connect groups yet; it has no `Failed to connect
  to` line.
