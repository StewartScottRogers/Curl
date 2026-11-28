# ADR-1472 — A followed redirect hop that ends with exit 28 counts the chain's times from `-m`'s start

- Status: Accepted
- Date: 2026-11-28
- Decided by Claude under Stewart's delegation
- Task: BL-6092 (follow-up of ADR-1470, fitness finding STD-231)

## Context

Against a 302 to a listener that never answers, `curl -sS -L -m 2 -w "%{time_total}"` printed
`2.259170` from real curl and `1.965191` from Curl. With the 302 held back 1.2 s, real curl printed
`2.282599`, so `-m` limits the whole chain and `%{time_total}` is not under it. `RedirectFollower`
measured the merged total from the first hop's handler, which starts a few milliseconds after the
`-m` watchdog (`ITransferContext.OperationStarted`).

## Decision

When a followed hop ends with exit 28 and the first context has an `OperationStarted`,
`RedirectFollower` counts the merged timings from it: `Started` moves there and
`RedirectDuration` grows by the same gap. Every phase stays the instant it was, as in ADR-1470.
After the fix the repro printed `2.017560` to `2.028001`.

A chain that ends any other way, or without `-m`, keeps the first handler's start, as ADR-1470 keeps
it for a first hop. A first hop that ends with exit 28 needs nothing here: the engine already counts
it from `-m`'s start.

## Consequences

This replaces the last consequence of ADR-1470: a redirect chain `-m` ends is no longer a few
milliseconds under `-m`.
