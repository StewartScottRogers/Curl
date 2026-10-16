---
id: BL-2479
title: Let ManagedEasyTransfer perform from the options through the curl tool's follower and context factory
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2479 — Let ManagedEasyTransfer perform from the options through the curl tool's follower and context factory

## Goal

A `ManagedEasyTransfer` given an internal tool-dispatch hook performs from its options through the curl tool's own `RedirectFollower` (the option group's `TransferDispatch`: its handlers, connectors, pool, run cookie store and `--resolve` loader) and the runner's `TransferContextFactory` (its standard input for `telnet` uploads, its platform credential encoding, its run diagnostic log and HSTS store), in place of the dispatcher `createDispatcher` builds and the `new TransferContextFactory(Stream.Null, ...)` in `CreateContext`.

## Context

ADR-0506 phase 2, ADR-0552, ADR-0554/0558/0561/0567. BL-2394 (switch `CurlCommandRunner.FollowRetryingThroughAsync` from `ToolAttempt` to a perform from the options with `ToolInputs`, `ToolOutputs`, `ToolEvents` and `ToolProgress` set, then delete `ToolAttempt` and `IToolAttemptTransfer`) found on its 2026-10-16 claim that none of BL-2395 to BL-2398 covers the dispatcher or the context factory: a perform from the options always builds its follower with `createDispatcher(options, PerformShared, ...)` (in the runner, `GivenOrManagedEngine`'s real-network dispatcher, not the group's dispatch that tests inject fake connectors into, and not the run's shared `ConnectionCache`, so `%{conn_id}` reuse across URLs and groups would break) and its context with a factory that has no standard input and the default credential encoding. Add one internal per-transfer hook in the style of the others (e.g. `ManagedEasyTransfer.ToolDispatch`, an `EngineToolTransferDispatch(RedirectFollower Follower, TransferContextFactory ContextFactory)` record), decide it in an ADR, not copied by `Duplicate` and cleared by `Reset`. Also give the runner what it reads off the attempt's context today without building one: the parsed URL the context carried (the `TransferRetrier` and `KeepRetriedConnectionIdWhenReused` read `context.Url`), e.g. by exposing the last perform's context or its URL beside `LastResult`.

## Acceptance criteria

- [ ] With the hook set, a perform from the options runs through the given follower (a fake handler behind it sees the request) and the context carries the given factory's standard input and credential encoding; without it, `Curl.Api` and `Libcurl` handles behave as before (existing `ManagedEasyTransferTests` unchanged and green).
- [ ] `Duplicate` does not copy the hook and `Reset` clears it, each pinned by a test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the hook and why.
- [ ] `dotnet build` clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
