# ADR-1009: An extension transfer hook can repeat the transfer after a delay

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4174)

## Context

`--sse` (BL-4110) must reconnect when the server closes the stream, after the event stream's
reconnection time and with `Last-Event-ID` (WHATWG HTML §9.2.3 "Processing model"). ADR-1007's
`ITransferHook` only adds request headers, wraps the response body and reads the result, and
`TransferHookingProtocolHandler` runs curl's handler exactly once; an extension cannot reach the
handler itself.

## Decision

- `ITransferHook` gains a default-implemented member, `TimeSpan? RepeatAfter(TransferResult result)`,
  returning `null` (do not repeat). With no hook naming a delay every transfer runs exactly once, as
  before.
- After each run `TransferHookingProtocolHandler` flushes the body, lets every hook read the result
  (`TransferFinished`), then asks the hooks in request order; the first that names a delay decides it.
- It waits that delay with `Task.Delay` on the transfer's `ITransferContext.TimeProvider` and
  `CancellationToken` (never `Thread.Sleep`), then runs the handler again, asking every hook's
  `RequestHeadersFor` afresh so a hook can send what it learned (such as `Last-Event-ID`). The body
  streams the hooks gave are built once and reused, so one output receives every run's body.
- Cancelling the transfer during the wait ends it without an exception; the last run's result is the
  transfer's, so curl reports it (`-w`, exit code) as it would a single run.

## Consequences

- A hook decides when to stop repeating by returning `null`; Curl adds no repeat limit of its own,
  because SSE reconnects for as long as the user lets it run.
- The engine, the core and the protocols are unchanged; only the hooking handler loops.
