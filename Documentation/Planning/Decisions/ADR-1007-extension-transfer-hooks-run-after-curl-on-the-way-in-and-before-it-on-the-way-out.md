# ADR-1007: Extension transfer hooks run after curl on the way in and before it on the way out

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4094)

## Context

SSE, the HTTP cache, digests, checksums, HAR, OpenTelemetry, record and replay and OAuth all need to
add request headers, read the response status and headers, observe or rewrite the response body and
read the timings of a transfer. ADR-0999 lets a host give the engine extension protocol handlers, but
the engine, the core and every protocol may not reference an extension project
(`VerifyExtensionIsolation`), and with no extension option given curl's behaviour must not change.

## Decision

- `Curl.Extension.Abstractions` (folder `Transfer`, now referencing `Curl.Protocol.Abstractions`) holds
  `ITransferHook`, with three members that each do nothing unless an extension implements them:
  `RequestHeadersFor`, `ResponseBodyOver` and `TransferFinished`. `TransferHooks.ActivatedBy` keeps the
  hooks whose extension's option the option group gave; `TransferHooks.Around` wraps a protocol
  handler in them, and gives the handler back unchanged when there is none.
- The engine takes the narrowest seam, a `Func<IProtocolHandler, IProtocolHandler>`
  (`EngineComposition.CreateProtocolHandlers`'s `extensionTransferHooks`): a host passes
  `TransferHooks.Around`; `null`, the default, runs no hook. Every handler, curl's and the extensions',
  goes through it, inside the engine's end-point reporting.
- Order: on the way in, hooks run after curl's own processing. Their request headers are appended after
  curl's (`-H` and the rest), seeing the context as curl's options built it. On the way out, hooks run
  before curl's: the handler writes the decoded body through the hooks' streams (the first hook's
  first) before it reaches curl's output, and each hook reads the result (exit code, response code,
  headers, timings) before curl reports it with `-w` and the exit code. The outermost hook stream is
  flushed once the handler returns.
- A transfer that is not HTTP keeps no HTTP options, so a hook's request headers are ignored there.

## Consequences

- Without an extension option nothing is wrapped, so curl's transfers are byte for byte what they were.
- Extensions are written and tested against `ITransferHook` alone, with a fake handler and no network.
- The hosts' wiring (building `TransferHooks` from each option group's values) is later work, as is
  each extension's own hook.
