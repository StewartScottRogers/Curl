# ADR-1019: A transfer hook may answer a transfer itself or replace curl's response

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4204)

## Context

`--cache-dir` (BL-4133, RFC 9111) must serve a fresh stored response without contacting the origin
(§4.2) and, on a 304 Not Modified, send the stored response in its place (§4.3.4). ADR-1007's
`ITransferHook` could only add request headers, pass the body through a stream, read the result and
ask for a repeat (ADR-1009): `TransferHookingProtocolHandler` always ran curl's handler, and the body
stream was flushed before `TransferFinished`, so a hook never knew the status while it could still
change the output.

## Decision

- `ITransferHook` gains a default member
  `ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) => next(context)`.
- `TransferHookingProtocolHandler` runs it on each run of the transfer (each repeat included), the hooks
  outermost first in request order, curl's handler innermost. The `context` a hook is given already has
  every hook's request headers and its `Output` is the outermost body stream, so a response a hook writes
  still passes through every hook's `ResponseBodyOver`; the result the outermost hook returns is the
  one every hook's `TransferFinished` and `RepeatAfter` read, after the body streams are flushed.
- A hook answers itself by not calling `next`, writing to `context.Output`, `HeaderOutput` and
  `DumpHeaderOutput`. It replaces curl's response by calling `next` with
  `context.WithOutputs(body, headers, dumpedHeaders)` (the public `TransferContextOutputs` extension,
  which keeps every other member of the transfer), then writing what it chooses to the outputs it was given.

## Consequences

- With no hook overriding it, the default calls curl's handler on the same context, so a transfer's
  output and `-i`/`-D` header output are unchanged (every earlier test passes as it was).
- The hook, not the engine, decides what a replaced response writes; curl's `-w` and exit code then
  report the hook's `TransferResult`.
