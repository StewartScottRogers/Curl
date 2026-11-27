# ADR-1453 — A cancellation just before the connect limit is the limit passing

- **Status:** Accepted
- **Date:** 2026-11-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6039.
Amends Decision 1 of ADR-0117.

## Context

curl 8.18.0 on Linux answers `curl -sv -m 3 http://192.0.2.20:1/` with
`* Connection timed out after N milliseconds` before `* closing connection #0`. Curl printed
no such line there, though it did on Windows.

`TcpConnector` holds each connect to the smaller of `--connect-timeout` and `-m` (ADR-0117,
BL-510 amendment), and reported a cancellation as the timeout only once that limit had passed
on its own clock. `-m`'s clock starts when the attempt starts, before the connect does, so on
Linux, whose timers fire close to their due time, `-m`'s cancellation reaches the connector
while its limit still has that start-up time to run: up to about 150 ms in a freshly started
linux-x64 build (measured in WSL, BL-6039 Notes). Windows' 15.6 ms timer granularity hid it.
A Linux timer, running on the coarse monotonic clock, can also fire a few milliseconds before
the high-resolution clock reads its due time.

The connector is not told which of the caller's cancellations is `-m`.

## Decision

A connect cancelled in any of these cases ends with exit 28 and
`Connection timed out after N milliseconds` (or `Resolving timed out after N milliseconds`),
also reported as a `-v` line:

1. the connector's own limit timer fired, whatever its clock reads;
2. the clock reads at least the limit;
3. the clock reads within a quarter of the limit, at most 500 ms, of it: this is `-m`.

Any earlier cancellation still escapes. `N` is the time since the connect started, but never
less than the limit, since curl counts it from the start of the transfer and so never prints
less than the limit that passed.

## Consequences

- A cancellation for another reason (a lost race, the library's caller) in the last quarter of
  the limit, at most the last 500 ms, is reported as the timeout. Such a coincidence needs the
  limit to be about to pass anyway.
- A connect that starts well after `-m`'s clock, a redirect hop late in `-m`, still gets no
  `-v` line from the connector, on every platform. A follow-up task covers it, with the HTTP
  handler's error message, which counts its own `N` and can differ from the `-v` line's by the
  time the connect takes to unwind.

## Alternatives considered

- **Tell the connector `-m` and when the operation started.** Exact, but it changes
  `ConnectTarget` or the connector's construction for one `-v` line, and every handler would
  have to pass the start.
- **Treat every cancellation during a connect as the timeout.** Rejected: a lost `--http3`
  race would print a timeout that did not happen.
