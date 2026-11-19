---
id: BL-5573
title: Stop ManagedEasyWebSocket.WaitForDataAsync leaking a cancellation registration on every wait
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.WebSocket.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5573 — Stop ManagedEasyWebSocket.WaitForDataAsync leaking a cancellation registration on every wait

## Goal

`ManagedEasyWebSocket.WaitForDataAsync` leaves no registration on the caller's cancellation token once a wait ends, however many waits share one token.

## Context

Found by the shift-end review of 2026-11-19 (code-reviewer and silent-failure-hunter, also type-design-analyzer) in the diff 74adc46d10...29c53fa22a (BL-5559, ADR-1308).

- **Where:** `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:142`.
- **What the code does:** it waits with `await Task.WhenAny(pendingRead, Task.Delay(Timeout.Infinite, cancellationToken))`.
- **What goes wrong:** when the read wins, the infinite delay stays registered on `cancellationToken` until that token is cancelled or disposed. A port that follows the new `Libcurl/README.md` wait-then-recv loop with one application-lifetime token adds one registration per received message, so memory grows for the life of the connection.

## Acceptance criteria

- [ ] `WaitForDataAsync` waits with `pendingRead.WaitAsync(cancellationToken)`, or with a linked source it disposes, and creates no `Task.Delay(Timeout.Infinite, ...)`.
- [ ] A fault in the pending read does not escape from the wait. `ReceiveAsync` still reports it as `RecvError`, as it does today.
- [ ] Cancellation still ends the wait with `OperationCanceledException`, as today.
- [ ] A test in `Curl.Engine.UnitTests/ManagedEasyTransferTests.WebSocket.cs` pins that a faulted read ends the wait without throwing and that the following receive answers `RecvError`.
- [ ] `dotnet build` is clean, the fast tests pass, and `Curl.Engine.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: cf34896b 21590eb7 d2fb4523 49aa1c34 459d9b86.
