# ADR-1460 — The HTTP handler takes the connector's timed-out result, else reports the line itself

- **Status:** Accepted
- **Date:** 2026-11-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6071.

## Context

curl prints one `* Connection timed out after N milliseconds` and `curl: (28)` with the same N.
`HttpTransferDeadline` counted its own N after a limit ended a connect, so the two could differ,
and when the connector did not report the end of the connect (ADR-1457: a redirect hop that starts
well after `-m`'s clock) there was no `-v` line at all.

Measured with `Record-CurlExchange.ps1` on Windows (curl 8.22.0 and 8.23.0, Schannel):
`-svSL --trace-time -m 4` following a 302, answered late, to a blackholed address. Both print the
line once, and N counts from the start of the hop, so it is below `-m`. The hop's connect ends at
the first one-second tick at or after `-m`'s remaining time (2027 ms with 1.5 s left), and past
`-m` itself when the redirect came early (4025 ms after a 0.5 s first hop, `-m 4`), because curl
arms no timer for the remaining time.

## Decision

When a time limit ends a connect, `HttpTransferDeadline`:

1. returns the connector's result when the connector ended the connect with exit 28, since the
   connector has reported that message as the `-v` line;
2. else fails with `Connection timed out after N milliseconds`, N from the start of this request,
   and reports it as a `-v` line, unless the connect was abandoned because it lost a race.

Curl ends the hop when `-m` runs out. It does not copy curl's later end, an artefact of curl's
polling that overruns the limit the user gave.

## Consequences

- On the first request N is the connector's, never below the limit (ADR-1457). On a hop it is the
  time since the hop started, as in curl.
- A connector that takes more than the 100 ms unwind grace to report would leave two `-v` lines.
- When both attempts of an `--http3` race end unreported, each reports its line.
