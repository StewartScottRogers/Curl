# ADR-0561 — The easy handle takes the runner's -v and --trace events through an internal hook, not the debug callback

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2397.

## Context

BL-2394 retires the `ToolAttempt` hook (ADR-0552): the easy handle is to build each of the runner's
serial attempts from its options. The runner reports every attempt's events to its own
`ITransferEvents` chain: the `HoldableStream`-backed `-v`, `--trace` and `--trace-ascii` writer,
`TraceIdsTransferEvents` (the `--trace-ids` prefix), `MultiStateTraceEvents` (the `[MULTI]` lines),
its `--trace-config` decorators and `ConnectionIdRecordingTransferEvents`, which numbers `%{conn_id}`
on the runner's `RunningTransferState`.

The task offered two routes: carry those events through `EasyCallbackOptions.Debug` with a tool-side
adapter that rebuilds every line, or an internal events hook. The debug callback keeps libcurl's
signature (ADR-0543): a `CurlInfoType` and bytes. By the time an event becomes callback bytes it has
already been rendered as `TransferEventInfoText` lines, so the structured events the runner's writer
words itself (a connection opened or reused, a TLS handshake, trust or message, a certificate verify
code, TLS early data) arrive flattened, and the runner's `MultiStateTraceEvents` and recorder, which
act on the event kind, could only guess it back from text. An adapter would be a second renderer of
every `-v` line, kept byte for byte in step with the first: the opposite of the simplest thing.

The handle also numbers `%{conn_id}` itself (`EngineConnectionIdTransferEvents`, ADR-0547); with the
runner's recorder present it would number every connection twice.

## Decision

1. `ManagedEasyTransfer` gets an internal `ToolEvents`, an `EngineToolTransferEvents` record
   (`Events`, the runner's whole chain, and `State`, the running transfer its recorder numbers
   `%{conn_id}` on). While it is set, a perform from the options reports every event to `Events`
   alone - not to the debug callback, and without the handle's `TraceComponents` decorators, which the
   runner's chain already has - runs on `State` (its watchdogs and `%{conn_id}`), and builds no
   `EngineConnectionIdTransferEvents`, so `%{conn_id}` is numbered once, by the runner's recorder, and
   `Info.ConnectionId` is what it kept. `TlsVerifyResultRecordingTransferEvents` still wraps the chain,
   as it numbers nothing and only copies the verify codes onto `Info`.
2. Without it, as for every `Curl.Api` and `Libcurl` handle, the handle reports to the debug callback
   and numbers `%{conn_id}` as before. Like `ToolInputs` and `ToolOutputs` (ADR-0554, ADR-0558) it
   belongs to one transfer: `Duplicate` does not copy it and `Reset` clears it.

## Consequences

- BL-2394 sets `ToolEvents` from the events and state the runner already builds and drops one more
  argument group from its `ToolAttempt`; nothing in `Curl.Console` changes yet.
- The debug callback stays libcurl's and only libcurl's; the tool's events never pass through it.
