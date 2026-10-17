# ADR-0614 — The easy handle takes the curl tool's follower and context factory through one internal hook

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2479.

## Context

BL-2394 switches `CurlCommandRunner` from handing `ManagedEasyTransfer` a prepared `ToolAttempt` (ADR-0552) to a
perform from the options, with the runner's chosen pieces (`ToolInputs`, ADR-0554), outputs (`ToolOutputs`,
ADR-0558), events (`ToolEvents`, ADR-0561) and progress (`ToolProgress`, ADR-0567) set. Two pieces were still the
handle's own. A perform from the options built its follower over `createDispatcher(options, PerformShared, ...)` -
in the runner, the real-network dispatcher of `GivenOrManagedEngine`, not the option group's `TransferDispatch`
that tests inject fake connectors into and whose pool sits over the run's shared `ConnectionCache`, so `%{conn_id}`
reuse across URLs and groups would break - and its context with `new TransferContextFactory(Stream.Null, ...)`,
which has no standard input for a `telnet` upload and Latin-1, not the platform's, as the credential encoding. The
runner also reads the attempt context's parsed URL (`TransferRetrier`, `KeepRetriedConnectionIdWhenReused`), which a
perform from the options never handed back.

## Decision

1. **One more internal per-transfer hook, `ManagedEasyTransfer.ToolDispatch`**, an
   `EngineToolTransferDispatch(RedirectFollower Follower, TransferContextFactory ContextFactory)` record. While set,
   a perform from the options runs through `Follower` and builds its context with `ContextFactory` as it is - its
   standard input, encoding, diagnostic log and HSTS store are the runner's - and never calls `createDispatcher`.
   Both travel together because the runner builds them together per option group.
2. **Like the other per-transfer hooks, `Duplicate` does not copy it and `Reset` clears it**: it belongs to one
   transfer of one runner, and a `Curl.Api` or `Libcurl` handle never sees it.
3. **`ManagedEasyTransfer.LastContext`** is the context the last perform from the options built, `null` before
   one and after one that failed before building it, beside `LastResult`, so the runner reads `context.Url` from
   it. The whole context rather than only its URL, because it is what the runner held before and costs nothing.
4. Without the hook nothing changes for `Curl.Api` and `Libcurl` handles.

## Why not the alternatives

- **Putting the follower and factory on `EasyTransferHost`**: the host is the run's wiring and `Duplicate` copies
  it, but the follower is per option group and must not leak into a duplicated handle.
- **Passing the dispatcher factory per transfer**: the runner already owns a built `RedirectFollower` (with its hop
  proxy, alt-svc and credential selectors); rebuilding one in the engine would duplicate that wiring.
- **Public API**: nothing in libcurl's easy interface takes a dispatcher; this is the tool's alone, so internal.

## Consequences

BL-2394 can set `ToolDispatch` beside the other hooks and delete `ToolAttempt` and `IToolAttemptTransfer`. Tests:
`ManagedEasyTransferTests.ToolDispatch.cs`.
