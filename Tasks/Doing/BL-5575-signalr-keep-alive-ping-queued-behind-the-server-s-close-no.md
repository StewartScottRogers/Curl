---
id: BL-5575
title: SignalR keep-alive Ping queued behind the server's Close no longer fails a finished transfer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRHubSession.cs, Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRKeepAliveTests.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5575 — SignalR keep-alive Ping queued behind the server's Close no longer fails a finished transfer

## Goal

A SignalR transfer that read every record and the server's Close ends with exit 0 even when a keep-alive Ping was waiting to be written at that moment, and no Ping frame is ever written after a Close frame.

## Context

Found by the shift-end review of 2026-11-19 (silent-failure-hunter, confirmed by hand) in the diff d0f316b4ad...b9ca725077, commit for BL-5572.

- `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRHubSession.cs:56-71` now awaits the Ping task (`await pinging;`) after the read loop ends normally. `PingWhenIdleAsync` (:86-99) checks `stop` only before it starts a Ping and writes on `abort`, the transfer's own token, which a normal end of the read loop does not cancel.
- `Transport/SignalRWebSocketSession.cs` serialises frames on the `sending` semaphore. When the server's Close arrives, `AcceptRunAsync` answers it and sets `IsClosed`; a Ping already waiting on `sending` then writes a text frame after both Close frames (RFC 6455 section 5.5.1 forbids data after Close). If the server has shut the TCP connection the write throws, and `await pinging` rethrows it, so a transfer that delivered everything ends with an error exit. Before BL-5572 the Ping task was fire-and-forget and could not change the result.
- Second, smaller defect on the same lines: the comment says that when the read loop fails "the Ping task ends quietly", but `await pinging` is skipped on that path; the handler's `await using` disposes the connection while a Ping may still be writing, and that task's fault is never observed.

## Acceptance criteria

- [ ] After the server's Close has been answered (`SignalRWebSocketSession.IsClosed`), the keep-alive loop writes no Ping frame; a Ping that was queued behind the Close answer is dropped, not written.
- [ ] A Ping write that fails after the server's Close does not change the transfer's exit code (exit 0 when every record was read).
- [ ] A Ping write that fails while the hub is still open still fails the transfer with `RecvError`, as `PingWriteFailsWithAnIoError_FailsTheTransferWithTheReceiveErrorCode_HubProtocolPing` pins.
- [ ] When the read loop throws, the Ping task is awaited (its own failure discarded) before the method returns, so the connection is never disposed under a Ping write, and the read loop's exception is the one reported.
- [ ] The comments in `SignalRHubSession.cs` describe each path as the code now behaves.
- [ ] New tests in `Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRKeepAliveTests.cs` pin the first, second and fourth criteria, pass on Windows, Linux and macOS, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-19: Created by the shift-end review (review-20261119-152921).
- 2026-11-19: Backlog -> Doing. Criteria at claim: 32d41de2 9c122d8b 8165c082 c566fbe6 5e0b4a42 ae19c8cf.
