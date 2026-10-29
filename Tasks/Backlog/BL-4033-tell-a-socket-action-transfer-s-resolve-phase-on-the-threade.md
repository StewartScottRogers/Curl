---
id: BL-4033
title: Tell a socket-action transfer's resolve phase on the threaded resolver's wakeup socket and signal it when the lookup ends (CGR-034)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitLibrary/SocketActionGatedResolver.cs, Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Engine.UnitTests/TransferSocketWaitTests.cs, Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4033 — Tell a socket-action transfer's resolve phase on the threaded resolver's wakeup socket and signal it when the lookup ends (CGR-034)

## Goal

While a socket-action transfer resolves its host, the socket callback is told one socket with `CURL_POLL_IN` (the threaded resolver's wakeup socket), and the lookup's end signals it, so the application is brought back by a socket rather than by `ManagedMultiTransfer`'s 10 ms `RunningTransferCheckInterval` timer.

## Context

Prerequisite of BL-3990 (CGR-034), found by its 2026-10-28 run. `TransferSocketWait` has no resolve-phase state (`BeginConnect` starts at the dial, in `SocketActionGatedTcpDialer`), so a resolving transfer has no told socket and `ManagedMultiTransfer.RunningTimerMilliseconds` falls back to the 10 ms check. curl 8.22.0 with the threaded resolver and a socketpair tells the resolver's wakeup descriptor through the socket callback. Measure real curl (a libtest with `CURLMOPT_SOCKETFUNCTION` on a name that takes time to resolve) before pinning the callback order. `SocketActionGatedResolver.cs` is a suggested name for a resolver wrapper wired beside `SocketActionGatedTcpDialer` (the `wrapLookUps` parameter of `EngineComposition.CreateTransports`).

## Acceptance criteria

- [ ] A socket-action transfer that is resolving is told one socket with `CURL_POLL_IN`, and the lookup ending signals that socket, pinned by unit tests in `Curl.Engine.UnitTests`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
