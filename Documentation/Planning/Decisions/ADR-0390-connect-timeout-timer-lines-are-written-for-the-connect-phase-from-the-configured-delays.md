# ADR-0390 — The `[TIMER] [CONNECTTIMEOUT]` and `expires in` lines are written for the connect phase, from the configured delays

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1210, following ADR-0357 and BL-1186. curl 8.21.0 (Schannel), measured on 2026-10-02 with
`Record-CurlExchange.ps1` (BL-1210 Notes):

- With `--connect-timeout`, `--trace-config timer` writes `[TIMER] [CONNECTTIMEOUT] set for <µs>ns`
  before every connection filter's line, then, after the first attempt, the nearest timer's
  `gives multi timeout in <ms>ms` (the happy-eyeballs delay while it runs, else the connect timeout).
  After a second family starts on time the connect timeout gives the timeout again, less the delay.
- The multi writes each pending timer's `expires in <µs>ns` before its `gives` line, but only when
  `multi` is traced too (`timer,multi`, `network`, `all`, `-vvvv`).
- No `--connect-timeout` (or 0) writes no `CONNECTTIMEOUT` line; curl's 300-second default is silent.
- curl writes the connect timeout's `expires in`/`gives` lines again while it waits for the response,
  only when the response has not arrived by its first poll, which is racy on loopback.
- `-m` writes `[TIMER] [TIMEOUT]` lines, and a `-m` shorter than the connect timeout takes over the
  `gives` lines.

## Decision

1. `ConnectAttemptTraceEvents` writes the `CONNECTTIMEOUT` `set`, `expires in` and `gives` lines of a
   direct connect, the `[HAPPY_EYEBALLS] expires in` line, and names the nearest timer in `gives`.
   `TcpConnector.TracedConnectTimeout` carries the `--connect-timeout` given (not the connector's
   effective limit, which defaults to 300 s and may be cut by `-m`); `TracesTimerExpiry` is `multi`.
2. The volatile values are written from the configured delays as if no time had passed (ADR-0357):
   `expires in 1000000ns`, `gives multi timeout in 1000ms`, and after a 200 ms second family,
   `800000ns` and `800ms`. Milliseconds round up, as curl's do.
3. The `expires in` and `gives` lines follow the attempt's socket lines directly; curl writes a poll
   round between them under `network`, which Curl does not write there (BL-1186's same choice).
4. The response-wait lines and the `-m` `[TIMER] [TIMEOUT]` lines are left to a follow-up task.

## Alternatives considered

- Measuring elapsed time with `TimeProvider`: the nanosecond values differ on every run of real curl,
  so no test could pin them, and a drop-in replacement gains nothing from matching noise.
- Writing the response-wait lines after every request: real curl writes them only when the response
  is late, so writing them always would be wrong as often as writing them never.

## Consequences

`curl -v --trace-config timer --connect-timeout 1` and `network` now print curl's connect timeout
timer lines around a direct connect; `-m`'s timer and the response wait are not yet written.
