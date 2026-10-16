# ADR-0622 — The runner performs each serial attempt from the options through its hooks

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2394.

## Context

ADR-0552 had `CurlCommandRunner` hand `ManagedEasyTransfer` a prepared `ToolAttempt`, a context the runner built.
BL-2395 to BL-2398 and BL-2479 gave the handle hooks for every piece of that context (ADR-0554, ADR-0558, ADR-0561,
ADR-0567, ADR-0614). Retiring `ToolAttempt` left four questions: how the runner reads the whole `TransferResult`,
which URL the handle transfers, and how the handler sees exactly the events and token the runner's own context gave.

## Decision

1. The runner reads `LastResult` (ADR-0554) through `IToolEasyTransfer`, the internal interface that replaces
   `IToolAttemptTransfer`: the hooks and `LastResult`, so a test can wrap the handle and count performs.
2. `EngineToolTransferInputs.Url` carries the URL the runner parsed and checked; the handle transfers it in place of
   a parse of `EasyNetworkOptions.Url`, which still names the transfer in `Info`. Reparsing the runner's URL text could
   normalise it differently.
3. With `ToolEvents` set the handle passes the runner's events to the context as they are, without its
   `TlsVerifyResultRecordingTransferEvents` wrapper: the runner records the verify codes itself, and the handler and
   every `ConnectTarget` carry the runner's own sink, as before.
4. With `ToolProgress` set and a perform token that cannot be cancelled, the context carries the runner's abort token
   as it is, not one linked through a new source, so a handler sees an uncancellable token where the runner gave one.
5. A `-Z` attempt still builds its context in the runner and performs directly; both paths build from one
   `TransferAttemptPieces` record. The `--retry` loop gets a context of its own holding the URL, clock, diagnostic log
   and abort token, which is all `TransferRetrier` and `KeepRetriedConnectionIdWhenReused` read.

## Consequences

`ToolAttempt`, `IToolAttemptTransfer` and `ToolAttemptResult` are gone; the runner's output is unchanged (every fast
test and the conformance ratchet pass). `TransferPerformer.CreateRetryAttemptContext` is no longer called by the runner.
