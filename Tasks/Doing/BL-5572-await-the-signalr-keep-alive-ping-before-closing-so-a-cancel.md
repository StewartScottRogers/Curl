---
id: BL-5572
title: Await the SignalR keep-alive Ping before closing so a cancelled Ping never cuts a frame short
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRHubSession.cs, Curl.Extended.Protocol.SignalR.UnitTests/Client]
requirement: none
created: 2026-11-19
completed:
---
# BL-5572 — Await the SignalR keep-alive Ping before closing so a cancelled Ping never cuts a frame short

## Goal

`SignalRHubSession` waits for its keep-alive Ping task to stop before it sends Close, and a Ping frame that has started writing is always written whole.

## Context

Found by the shift-end review of 2026-11-19 (silent-failure-hunter, confirmed by code-reviewer) in the diff 74adc46d10...29c53fa22a (BL-5568's keep-alive).

- **Where:** `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRHubSession.cs:55-70`.
- **What the code does:** `_ = PingWhenIdleAsync(stopPinging.Token)` starts a task nobody awaits. The `finally` cancels `stopPinging` and goes straight on to `session.CloseAsync`.
- **What goes wrong:**
  - The same token is passed into `SendAsync`, so a cancel that lands mid-write stops a Ping frame halfway. `SignalRWebSocketSession`'s semaphore serialises whole sends but cannot repair a write that was cancelled partway. Over `SslStream` this leaves a truncated masked frame before the Close frame, so the hub reports a protocol error, or the next write throws, so a successful transfer exits non-zero.
  - A Ping that fails with `IOException` faults a task nobody observes. The comment says "the read loop reports it", which is true only if the read side fails too.
  - Code-reviewer also noted that `lastSentAt` is shared between the loop and the Ping task without `Volatile` or `Interlocked`.

## Acceptance criteria

- [ ] `RunAsync` keeps the Ping task, cancels it, and awaits it before `CloseAsync`; it catches only `OperationCanceledException`, and only from that cancellation.
- [ ] The stop token is checked only before a Ping send starts and is never passed into the frame write, so a cancel cannot stop a frame halfway.
- [ ] A Ping write that fails with `IOException` is reported as the transfer's failure (the code the read loop's transport failure maps to), not lost.
- [ ] `lastSentAt` is read and written with `Volatile` or `Interlocked`.
- [ ] Tests in `Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRKeepAliveTests.cs` pin each case:
  - a Completion arriving while a Ping write is blocked leaves a whole Ping frame followed by the Close frame;
  - a failing Ping write fails the transfer.
- [ ] `dotnet build` is clean, the fast tests pass, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: f5df8eca 254a99f2 a996f1ad c45ea4a6 1801f1ea a963b8b4.
