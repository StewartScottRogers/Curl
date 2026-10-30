---
id: BL-4344
title: Return the cancellation, not the stale result, when a transfer hook's repeat wait is cancelled
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferHookingProtocolHandler.cs, Curl.Extension.Abstractions.UnitTests]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4344 — Return the cancellation, not the stale result, when a transfer hook's repeat wait is cancelled

## Goal

When a transfer is cancelled during a transfer hook's repeat delay (`--max-time`, or the user aborting), the cancellation reaches the engine. The handler no longer returns the earlier failed result as if it were final.

## Context

Found by the silent-failure-hunter reviewer.

- `Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferHookingProtocolHandler.cs:82-93`: `WaitAsync` catches `OperationCanceledException` and returns `false`.
- `ExecuteAsync` (`:31-33`) then `break`s and returns the previous `result`. That result is a failure a hook had just asked to retry.
- The transfer's exit code is therefore the first attempt's failure, where it should be the timeout curl reports: exit 28 for `--max-time`.
- A cancellation is silently turned into an unrelated failure.

## Acceptance criteria

- [x] A cancelled repeat wait propagates `OperationCanceledException`, or otherwise yields the engine's normal cancellation outcome, and does not return the stale result.
- [x] A unit test in `Curl.Extension.Abstractions.UnitTests` cancels the context's token during a `RepeatAfter` delay, using a fake `TimeProvider`, and asserts that the handler does not return the earlier result.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

WaitAsync removed; Task.Delay now propagates OperationCanceledException (TaskCanceledException) to the engine. Test renamed and asserts it throws. Quality run: 0 failing members.

## Log

- 2026-10-30: Created by the shift-end review (ADR-0710) of a9f1a8149..b79175cc5.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 009dfb0c a5207319 3d836d5e.
- 2026-10-30: Doing -> Done. A cancelled transfer-hook repeat wait now propagates the cancellation instead of returning the stale result
