---
id: BL-4174
title: Let an extension transfer hook repeat the transfer after a delay
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer/ITransferHook.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferHookingProtocolHandler.cs, Curl.Extension.Abstractions.UnitTests/Transfer/TransferHookingProtocolHandlerTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4174 — Let an extension transfer hook repeat the transfer after a delay

## Goal

An extension's `ITransferHook` can ask, once a transfer ends, for the same transfer to run again after a delay it names (with its request headers asked again), so `--sse` can reconnect with `Last-Event-ID` after `retry` milliseconds.

## Context

Found by BL-4110 (Server-Sent Events, WHATWG HTML "Server-sent events" §9.2.3 "Processing model": on a dropped connection the user agent waits the reconnection time and reconnects with `Last-Event-ID`). ADR-1007's `ITransferHook` only adds headers, wraps the body and reads the result; `TransferHookingProtocolHandler` runs the inner handler exactly once, and an extension cannot reach the handler itself. Add a default-implemented member to `ITransferHook` (for example `TimeSpan? RepeatAfter(TransferResult result)`, `null` by default meaning do not repeat) and have `TransferHookingProtocolHandler` wait that long on the context's `TimeProvider` (never `Thread.Sleep`, honour the cancellation token) and run the handler again, asking `RequestHeadersFor` afresh each time, with the body streams built once. Record the decision in an ADR (or extend ADR-1007 by a new ADR) marked "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [x] `ITransferHook` has a default-implemented member by which a hook asks for the transfer to be repeated after a delay; with no hook asking, every transfer runs exactly once, unchanged (unit test).
- [x] `TransferHookingProtocolHandler` waits the delay on the context's `TimeProvider`, then runs the handler again with the hooks' request headers asked afresh; cancellation ends the wait (unit tests with a fake time provider, no network).
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the member and its semantics.
- [x] `Curl.Extension.Abstractions.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: default member `TimeSpan? RepeatAfter(TransferResult result) => null` on `ITransferHook`; `TransferHookingProtocolHandler` loops: run, flush, `TransferFinished` for each hook, then the first hook in request order naming a delay decides it; waits with `Task.Delay` on the context's `TimeProvider` and `CancellationToken`, then reruns with `RequestHeadersFor` asked afresh; body streams built once. Recorded in ADR-1009.
- Default taken: cancellation during the wait ends the transfer without an exception and the last run's result is returned, so curl reports it as a single run; no repeat limit (the hook stops by returning null).
- Measure-CodeQuality.ps1 -Library Curl.Extension.Abstractions.UnitLibrary: 0 failing members. Full build clean; fast tests green. Tests are pure in-memory (fake TimeProvider, no paths or OS text), so platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 73931ca5 19c0926a 8c59d058 e7c4212f 3d836d5e.
- 2026-10-29: Doing -> Done. An ITransferHook can ask, via RepeatAfter, for the transfer to repeat after a delay on the context's TimeProvider, with headers asked afresh
