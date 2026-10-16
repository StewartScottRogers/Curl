# ADR-0552 — `Curl.Console`'s serial attempts perform through an easy handle, carrying the runner's prepared attempt

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2318.

## Context

ADR-0506's phase 2 step 5 re-bases `Curl.Console` onto the engine seam: its serial transfers should
perform through `ICurlEngine.CreateEasy()` and `IEasyTransfer.PerformAsync` rather than calling
`TransferPerformer` directly. BL-2316, BL-2317, BL-2359, BL-2360 and their follow-ups gave
`ManagedEasyTransfer` most of what the runner's attempt needs, but the runner's attempt still carries
far more than the easy options describe: the proxy and credentials it chose, the SSH options and
known-hosts result, the `--etag-compare` header lines, the transfer's alt-svc cache, the `-J` header
watcher, `--limit-rate`, `-N` and `-B` output wrappers, styled `-i` headers on a terminal, the progress
recorder whose meter is written live, and the `-Y`/`-y` and `-m` watchdogs kept on the shared
`RunningTransferState`. Its `--retry` loop, failure lines and `-w` output read the attempt's whole
`TransferResult`, of which `TransferInfo` carries only part. Rebuilding all of that inside the engine
from the easy options and callbacks, byte for byte, does not fit one task.

## Decision

1. The runner takes an `ICurlEngine` (`CurlCommandRunner`'s `curlEngine`; `CurlComposition.CreateRunner`
   passes a `ManagedCurlEngine` built with curl's easy error text, and a runner given none makes one on its
   own clock). Each serial transfer takes one easy handle, sets its `Options` with
   `EasyOptionsFromCommandLine.EasyOptionsOf` (every group, the transfer's URL as `CURLOPT_URL`), and
   performs each attempt, `--retry` attempts included, with one `PerformAsync`; the handle is disposed
   when the transfer ends.
2. The handle performs the attempt the runner set up: `Curl.Engine`'s internal `IToolAttemptTransfer`
   (which `ManagedEasyTransfer` implements) has a `ToolAttempt` - the running transfer's state, the
   follower, the attempt's context and the redirect policy - that the next `PerformAsync` performs in
   place of one built from the options, under the same watchdogs (`TransferPerformer.FollowWatchingSpeedAsync`),
   clearing it first. The handle keeps the attempt's whole result as `ToolAttemptResult`, which the runner
   reads, and fills `ErrorText` and `Info` from it as for any perform.
3. `-Z` transfers keep performing directly until the engine's multi handle lands (ADR-0506, phase 3).
4. Moving the attempt's wiring into the handle - so the body and headers reach the runner through the
   write and header callbacks, `-v` through the debug callback and the meter through xferinfo - and
   retiring `ToolAttempt` is BL-2394.

## Consequences

- Every serial attempt now passes through `IEasyTransfer.PerformAsync` on the managed engine, which a
  recording `ICurlEngine` proves (`CurlCommandRunnerEasyTransferTests`), and the output is unchanged:
  the attempt itself is the same code on the same context.
- The goal's "output through the write and header callbacks" is not met yet; BL-2394 owns it.
- A test or host that gives the runner its own engine must hand out handles that implement
  `IToolAttemptTransfer`.
