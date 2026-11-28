# ADR-1470 — `%{time_total}` of a transfer `-m` ends counts from `-m`'s start, and `-m` ends an HTTP transfer only once it has passed

- Status: Accepted
- Date: 2026-11-28
- Decided by Claude under Stewart's delegation
- Task: BL-6082 (fitness finding STD-231)

## Context

curl prints a `%{time_total}` of at least the `-m` value for an `https` transfer whose TLS handshake
`-m` ends (`t=2.0x` under `-m 2`, measured, BL-6028 Notes). Curl printed 1.97 to 1.99. The HTTP
handler times itself, so the engine kept its start, which is later than the `-m` watchdog's
(ADR-1469 left this to BL-6082). Its own `-m` timer could also fire before the limit had passed on
the transfer's clock: .NET timers are coarser than the timestamps and drop part of a millisecond.

## Decision

- When a first hop ends with exit 28 and its handler reported timings, the engine moves their start
  to the `-m` watchdog's start (`ITransferContext.OperationStarted`), as it already counts a transfer
  it times itself (ADR-1469). Every other time in them stays the instant it was, so each is counted
  from the same start, as curl counts them all from the transfer's start.
- `HttpTransferDeadline` sets a `-m` timer that fires early again for the time left, as
  `MaxTimeWatchdog` does, with the wait rounded up to whole milliseconds.

Moving the start back to `completed - MaxTime` was rejected: the engine cannot tell from the result
whether `-m` or `--connect-timeout` ended the transfer, and the second must keep its shorter total.

## Consequences

Without `-m` nothing changes. A redirect hop `-m` ends still times from its own start, so under `-L`
the merged total starts at the first hop's handler and can be a few milliseconds under `-m`.
