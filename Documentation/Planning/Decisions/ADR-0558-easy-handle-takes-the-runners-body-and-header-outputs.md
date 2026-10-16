# ADR-0558 — The easy handle takes the runner's body and header outputs, and a failing write callback says what it returned

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2396.

## Context

BL-2394 retires the `ToolAttempt` hook (ADR-0552): the easy handle is to build each of the runner's
serial attempts from its options. The runner hands `TransferContextFactory.Create` four output pieces
the easy options cannot describe: the body stream, already chained through `--limit-rate`, `-N`'s flush
after every write, `--use-ascii`'s text mode and `StandardOutputFailureDeferringStream` or
`DeferredOutputFileStream`, whose write failures give its exit 23 texts; the `-D` stream; the `-J`
watcher (`RemoteHeaderNameStream`), which must see each header line before the header output does; and
the `--styled-output` header styles. The factory still has to wrap the body in the attempt's low-speed
watchdog, tee `-i` lines with `-D` (`HeaderLineTeeStream`) and hand the chosen header output to the
watcher. A write or header callback cannot carry that: a callback sees bytes after the factory has
chosen where they go, so it cannot stand in for a stream the factory wraps or tees.

`CallbackWriteStream` also failed a short write with a bare `IOException`, so the HTTP handler reported
`passed N returned 0` whatever the callback returned, where libcurl says what it returned.

## Decision

1. `ManagedEasyTransfer` gets an internal `ToolOutputs`, an `EngineToolTransferOutputs` record
   (`Output`, `HeaderOutput`, `WatchHeaderOutput`, `BodyHeaderStyles`). While it is set, a perform from
   the options passes the four to `TransferContextFactory.Create` as the runner's own call does, in place
   of the write and header callbacks, so the bytes, their order and the write failures are the runner's.
   Without it, as for every `Curl.Api` and `Libcurl` handle, the handle builds the same record from
   `EasyCallbackOptions.Write` and `Header` (`OwnOutputsOf`) and behaves as before. Like `ToolInputs`
   (ADR-0554) it belongs to one transfer: `Duplicate` does not copy it and `Reset` clears it.
2. `CallbackWriteStream` fails a short write with `OutputWriteFailedException`, its `BytesAccepted` the
   callback's return clamped to zero or more and its message
   `Failure writing output to destination, passed N returned M` with the return as given, so a failing
   callback ends the transfer with the runner's exit 23 text.

## Consequences

- BL-2394 sets `ToolOutputs` from the runner's existing stream chain and drops one more argument group
  from its `ToolAttempt`; nothing in `Curl.Console` changes yet.
- An internal stream hook, not a callback, is the seam for the tool's outputs; the public callbacks stay
  libcurl's.
