---
id: BL-5173
title: Tell transfer hooks the exit code the transfer actually ends with when a hop throws or is cancelled
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-5172]
touches: [Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitTests/TransferPerformerTests.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs]
requirement: none
created: 2026-11-14
completed:
---
# BL-5173 — Tell transfer hooks the exit code the transfer actually ends with when a hop throws or is cancelled

## Goal

When a hop throws or is cancelled, the failure `TransferPerformer` hands the handler's `TransferChainFinishedAsync` (and so every transfer hook, OpenTelemetry's span included) carries the exit code the transfer then ends with, not a guess.

## Context

Found by the shift-end review of 6320f85c2...208e836ac (the reviewer's own read; code-reviewer and silent-failure-hunter raised the cancellation half as a suggestion), in BL-5170's change.

`FinishedByExceptionAsync` (`Curl.Engine.UnitLibrary/TransferPerformer.cs:202-211`) tells the handler exit 42 (`AbortedByCallback`) for every `OperationCanceledException` and exit 2 (`FailedInit`) for any other exception, before the caller decides the real ending:

- `ManagedEasyTransfer.FollowUnlessAbortedAsync` (`Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:355-380`) turns an `IOException` from an aborted read callback (SMTP after `DATA`, upstream test1507, BL-2640) into `CURLE_ABORTED_BY_CALLBACK` (42), while the hooks were told exit 2.
- An `OperationCanceledException` that is not a callback or abort-token abort (a `TaskCanceledException` from an internal timeout, with `context.CancellationToken` not cancelled) is told as exit 42.

The hook then exports a span (OpenTelemetry) or records a result whose exit code differs from the one curl reports, which is what the hook exists to record. Decide the told code from what the caller will answer: for example map the exception the same way the callers do (a shared mapping both use), check `context.CancellationToken.IsCancellationRequested` before calling a cancellation an abort, or let the caller tell the chain end once it has its final code. Record the choice in the `FinishedByExceptionAsync` doc comment.

## Acceptance criteria

- [ ] A unit test with a read-callback upload whose callback aborts and a handler that lets the read's `IOException` escape shows the handler is told exit 42, the code the perform answers.
- [ ] A unit test with a hop that throws `OperationCanceledException` while the transfer's token is not cancelled shows the handler is not told exit 42.
- [ ] The existing BL-5170 tests (`FollowWatchingSpeedAsync_AbortTokenCancelled_...`, `FollowWatchingSpeedAsync_HopThrows_...`) still pass or are updated to the new mapping with the reason in the test name.
- [ ] The doc comments of `FinishedByExceptionAsync`, `ITransferHook.TransferChainFinishedAsync` and `IProtocolHandler.TransferChainFinishedAsync` say which exit code each ending is told with, and stay true of the code.
- [ ] `dotnet build` is clean and the fast tests pass; `Curl.Engine.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: da961aef 70d5e5a5 4856b334 5a7d9fbe d911a7e9.
