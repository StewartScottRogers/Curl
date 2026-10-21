# ADR-0729 — Unit program 1323 has no counterpart; the Limits batch's ports of 1303, 1399 and 3216 are tasks of their own

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2848
- Decided by Claude under Stewart's delegation.

## Context

ADR-0711 Decision 3 gives the Limits batch (BL-2848) seven upstream `unittest` cases: ports of 1303,
1323, 1399, 1605, 1606 and 3216, and a `no counterpart` record for 3214. It lets a batch that reads a
C program and finds the Curl type absent change its own row. Reading curl 8.22.0's
`tests/unit/unit1303.c`, `unit1323.c`, `unit1399.c` and `unit3216.c` against Curl found:

- **1323** checks `curlx_timediff_ms`, the millisecond difference of two `struct curltime` values
  (`tv_sec` and `tv_usec`) with C's truncating division per field, four rows. Curl has no such
  struct and no function over one: every elapsed time is `TimeProvider.GetElapsedTime` over
  `long` timestamps, a `TimeSpan`, so the arithmetic the program checks exists only in C.
- **1303** checks `Curl_timeleft_ms`, one function that answers the time left from `-m` and
  `--connect-timeout` together, with the 300 s default connect timeout. Curl keeps the two limits
  apart (`MaxTimeWatchdog` and each connector), so the port needs a new function in
  `Curl.Core.UnitLibrary` first.
- **3216** checks `lib/ratelimit.c`'s token bucket (rate, burst, block, a negative balance).
  Curl's `--limit-rate` is `RateLimitedStream`, which waits by bytes moved and has no burst or
  block, so the port needs a new rate limit type in `Curl.Core.UnitLibrary` first.
- **1399** checks that `Curl_pgrsTime`'s phase timers add up across redirect hops; finding where
  Curl sums them, and fixing it if it does not, is work of its own in `Curl.Output` or the redirect
  follower.

## Decision

- Row 1323 becomes `no counterpart (BL-2848 records it)`: `struct curltime` arithmetic, a C data
  structure Curl does not have; Curl measures elapsed time as `TimeSpan` from `TimeProvider`.
  Like 3214, the reason is lasting.
- BL-2848 ports 1605 (`CurlStrings.Escape` and `Unescape` given length -1, `Libcurl`) and 1606
  (`LowSpeedWatchdog` stepped second by second on a hand-moved `SteppedTimeProvider`) and holds
  both on the ratchet.
- 1303, 1399 and 3216 stay `port` rows, now owned by BL-2953, BL-2954 and BL-2955, each of which
  builds the Curl counterpart it needs and ports the program over it.

## Consequences

- ADR-0711's counts move to 62 ported and 22 `no counterpart`; its table rows for 1303, 1323,
  1399 and 3216 are read with this ADR.
- The gap office keeps 1323 and 3214 `excluded` with the reasons above; 1605 and 1606 measure as
  `match` from the next run.
