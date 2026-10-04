# ADR-0404 — The conformance server skips its waits unless a curl timer races them

- **Status:** Accepted
- **Date:** 2026-10-03
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

`UpstreamCaseRunner` ran every case's in-memory `sws` emulation (`SwsHttpServerConnector`) on
`TimeProvider.System`. A case's `writedelay: N` and `<postcmd>` `wait N` were therefore real
waits: upstream `test1677`'s `writedelay: 500` over an 11-write reply took 5.2 s of real time,
one timer wake after another. With nine dark factory lanes building and testing at once, those
wakes come late and the run outgrows its limits, so `test1677` failed lane 1's integration of
BL-1333 twice while passing on a quiet machine (BL-1355).

A `writedelay` exists so each write reaches curl in a read of its own. Only a timer on curl's
side can tell how long the wait really took: `--max-time`, `--connect-timeout`, `--speed-time`,
`--speed-limit` and `--expect100-timeout` race the server (test29's two-second `-m` against a
ten-second `<postcmd>` wait, BL-1321).

## Decision

When the case's arguments name none of those options (`CurlTimerOptions.AnyIn`, which also
reads `-m`, `-y` and `-Y` inside a bundle of short options), the runner gives the emulation a
`WaitSkippingTimeProvider`: a clock that stands still until a timer is made, then moves on by
the timer's due time and fires it at once. The emulation's own logic is unchanged - it already
waits on its injected clock until each write's moment - so writes keep their order and their
separate reads, and the case takes no real time. A case naming a timer option keeps the real
clock.

## Consequences

- `test1677` runs in about 0.25 s instead of 5.2 s, and no case without a curl timer depends on
  machine load for its server's timing.
- `--write-out` time variables in such a case measure no server wait; no listed case pins them.
- A case with a curl timer still waits in real time, as before; curl's own waits (such as
  `--retry-delay`) are untouched.

## Alternatives considered

- **A larger time limit.** Moves the threshold, not the dependence on load; the task rules it out.
- **Skipping write delays inside `SwsHttpServerConnection`.** Changes the emulation's documented
  timing for every caller and its own tests, including the cases where `-m` races the server.
- **Virtual time for every case.** Breaks cases like test29, where curl's real `-m` must fire
  before the server's wait ends.
