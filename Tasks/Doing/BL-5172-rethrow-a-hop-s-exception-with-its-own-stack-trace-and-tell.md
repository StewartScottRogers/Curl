---
id: BL-5172
title: Rethrow a hop's exception with its own stack trace and tell the chain end at most once
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitTests/TransferPerformerTests.cs]
requirement: none
created: 2026-11-14
completed:
---
# BL-5172 — Rethrow a hop's exception with its own stack trace and tell the chain end at most once

## Goal

An exception a hop throws leaves `TransferPerformer.FollowWatchingSpeedAsync` with its original stack trace, and the transfer's handler is told the chain finished exactly once, as `ITransferHook` and `IProtocolHandler` document.

## Context

Found by the shift-end review of 6320f85c2...208e836ac (code-reviewer, silent-failure-hunter, and the reviewer's own read), in BL-5170's change.

- `Curl.Engine.UnitLibrary/TransferPerformer.cs:210` (`FinishedByExceptionAsync`) ends with `throw exception;`, which resets the stack trace to the helper. The doc comments BL-5170 added (`ITransferHook.cs:79`, `IProtocolHandler.cs:30`) say the exception is "rethrown unchanged", and BL-5170's criterion says the exception is unchanged; its message and type are, its stack trace is not. A failure deep in a protocol handler now points at `FinishedByExceptionAsync`.
- The new `catch (Exception exception)` at `TransferPerformer.cs:194` also catches an exception thrown by `RedirectFollower.FollowAsync`'s own `NotifyChainFinishedAsync` (`Curl.Core.UnitLibrary/RedirectFollower.cs:202`), which runs after the chain already produced a result. A handler whose `TransferChainFinishedAsync` throws (the hook wrapper swallows hook exceptions, but a handler's own override or the dispatcher does not) is then told a second time, with a made-up exit 2.
- If `NotifyChainFinishedAsync` throws inside `FinishedByExceptionAsync`, that exception replaces the hop's original one, which is lost.

Fix: rethrow with `ExceptionDispatchInfo.Capture(exception).Throw()` (or a bare `throw;` from a filtered catch), and make the second notification impossible - for example notify only when the follower has not (a flag the follower sets, or catching only exceptions that left before the follower's own notification) - and keep the hop's exception when the notification itself fails.

## Acceptance criteria

- [ ] A unit test shows that the exception rethrown after a hop throws carries the hop's frame in its `StackTrace` (the throwing handler's method name appears in it).
- [ ] A unit test with a handler whose `TransferChainFinishedAsync` throws after a successful chain shows the handler is told exactly once.
- [ ] A unit test with a handler whose `TransferChainFinishedAsync` throws while a hop's exception is being reported shows the hop's exception, not the notification's, leaves `FollowWatchingSpeedAsync`.
- [ ] The doc comments of `ITransferHook.TransferChainFinishedAsync` and `IProtocolHandler.TransferChainFinishedAsync` stay true of the code.
- [ ] `dotnet build` is clean and the fast tests pass; `Curl.Engine.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: c83cc918 7a17321d 7f2ea65a 2cb2dc25 d911a7e9.
