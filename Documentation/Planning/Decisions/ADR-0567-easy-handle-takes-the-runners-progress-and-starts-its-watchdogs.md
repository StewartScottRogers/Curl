# ADR-0567 — The easy handle takes the runner's progress meter through an internal hook and starts the -Y/-y and -m watchdogs itself

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2398.

## Context

BL-2394 retires the `ToolAttempt` hook (ADR-0552): the easy handle is to build each of the runner's
serial attempts from its options. For each attempt the runner's context carries its progress meter
(`RunningTransferState.Progress`, a `TransferProgressRecorder` that feeds the meter lines and the `-#`
bar), the `-Z` `--fail-early` abort token, and the `-Y`/`-y` and `-m` watchdogs it starts on the running
transfer with `TransferPerformer.StartLowSpeedWatchdog` and `StartMaxTimeWatchdog` as it creates the
context, so each `--retry` attempt gets fresh ones (ADR-0117).

The task offered two routes for the meter: `EasyCallbackOptions.TransferInfo` with a tool-side adapter,
or an internal progress hook. The xferinfo callback carries four counts. The recorder takes every
`ITransferProgress` report: the transfer's start, a retried request and a resend on a fresh connection
(each restarts the meter's clock and counts), the counts with a total that may be unknown (`null`, which
the callback flattens to 0, the same as a known empty body), and the transfer's end. None of those but
the counts survives the callback, so an adapter could not redraw the runner's meter byte for byte.

## Decision

1. `ManagedEasyTransfer` gets an internal `ToolProgress`, an `EngineToolTransferProgress` record
   (`Progress`, the runner's recorder, and `AbortToken`, its `--fail-early` token). While it is set, a
   perform from the options reports progress there alone - neither the xferinfo nor the progress
   callback is called, so nothing aborts by callback - links `AbortToken` into the transfer's token, and
   starts the `-Y`/`-y` and `-m` watchdogs from `EasyTransferOptions` on the running transfer (the
   runner's state from `ToolEvents`, else the handle's own), through the engine's `TransferPerformer` on
   the engine's clock, writing to `Host.DiagnosticLog`. Every perform starts fresh ones, so a `--retry`
   attempt the runner performs again gets a fresh `-m`. A stalled attempt ends through
   `TransferPerformer.FollowWatchingSpeedAsync` with the same exit 28 text as through `ToolAttempt`.
2. Without it, as for every `Curl.Api` and `Libcurl` handle, the handle reports to its callbacks and
   starts no watchdog, as before; honouring `CURLOPT_TIMEOUT` and `CURLOPT_LOW_SPEED_*` on those handles
   is a change of their behaviour and not this step's. Like `ToolEvents` (ADR-0561) it belongs to one
   transfer: `Duplicate` does not copy it and `Reset` clears it.

## Consequences

- BL-2394 sets `ToolProgress` from the recorder and abort token the runner already has, and stops
  starting watchdogs for attempts the handle performs; nothing in `Curl.Console` changes yet.
- The diagnostic log the watchdogs write to is the handle's `Host`'s, which the runner sets to its own.
