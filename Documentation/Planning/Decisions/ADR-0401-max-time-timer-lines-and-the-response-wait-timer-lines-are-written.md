# ADR-0401 — `-m`'s `[TIMER] [TIMEOUT]` lines and the response wait's timer lines are written

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1258, following ADR-0390 (BL-1210). curl 8.21.0 (mingw, Schannel), measured on 2026-10-02 with
`Record-CurlExchange.ps1` against `http://127.0.0.1:P/` (BL-1258 Notes):

- `-v --trace-config timer -m 5`: `[TIMER] [TIMEOUT] set for 4999999ns` (or `5000000ns`) first,
  `[TIMEOUT] gives multi timeout in 5000ms` after `Trying`, and again after `Request completely sent off`.
- `-m 5 --connect-timeout 1`: both `set` lines, `TIMEOUT` first; `gives` names `CONNECTTIMEOUT` (1000ms).
  `-m 1 --connect-timeout 5`: both `set` lines still; `gives` names `TIMEOUT` (1000ms).
- Under `network`, `expires in` lines come for every pending timer, nearest first, before `gives`.
  While waiting for the response they sit between `[MULTI] [PERFORMING] pollset[fd=N IN], timeouts=<count>`
  and `multi_wait(fds=1, timeout=1000) tinternal=<nearest ms>`; the connect phase's poll lines carry
  the same `timeouts=` and `tinternal=`, which are `0` and `-1` with no timer.
- `localhost` (two families) with `-m 5`: after the IPv4 attempt, `[TIMEOUT] gives ... 4791ms`.
- The response-wait lines came in every one of the seven loopback runs measured here and in BL-1210.
  curl writes them only when the response has not arrived by its first poll; on loopback the server
  still has to read the request first, so in practice it never has.

## Decision

1. `ConnectAttemptTraceEvents` takes the `-m` given (`TcpConnector.TracedTransferTimeout`, set by
   `CurlComposition` from a positive `-m`) and treats `[TIMEOUT]` as one more pending timer beside
   `[CONNECTTIMEOUT]`: `set` lines in that order, `expires in` lines nearest first, the nearest named in
   `gives`, each less a second family's delay once it started on time.
2. The response-wait lines are always written after `Request completely sent off` when a `-m` or
   `--connect-timeout` is given: the nearest timer's `gives`, with every timer's `expires in` first under
   `multi` (`TransferTimers.WaitLines`). Without `multi`, `ResponseWaitTimerTraceEvents` writes them after
   that line; with it, `MultiStateTraceEvents` writes them between its `PERFORMING` poll lines.
3. Values are the configured delays, as ADR-0357 and ADR-0390 record: `5000000ns`, `5000ms`, and
   `4800ms` after a 200 ms second family, where curl's elapsed clock gave `4791ms`. The response wait's
   values are the configured delays too; the connect timeout stays pending, as curl does not clear it.
4. `MultiStateTraceEvents`' poll lines give `timeouts=` as the number of given timers and `tinternal=` as
   the nearest one's milliseconds (`-1` with none), whether or not `timer` is traced.

## Alternatives considered

- Never writing the response-wait lines (ADR-0390's position): every measured run wrote them, and over
  a real network a response is later still, so leaving them out is the more often wrong choice.
- Measuring elapsed time for the values: they differ on every run of real curl, so no test could pin
  them, and a drop-in replacement gains nothing from matching noise.

## Consequences

`curl -v --trace-config timer -m 5` (and `network`, `all`, `-vvvv`) prints curl's `[TIMER] [TIMEOUT]`
lines and the response wait's timer lines. A response that arrives before curl's first poll makes real
curl skip those lines where Curl writes them; that difference is accepted.
