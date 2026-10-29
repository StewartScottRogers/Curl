---
id: BL-3908
title: Stabilise ConnectAsync_ClosedPort_CallsOpenWithTheAddressThenCloseOnceAndFailsWithCouldntConnect under a full Engine test run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitTests/Cgr036Tests.cs, Curl.Engine.UnitTests/MultiCallbackQueueTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3908 — Stabilise ConnectAsync_ClosedPort_CallsOpenWithTheAddressThenCloseOnceAndFailsWithCouldntConnect under a full Engine test run

## Goal

`Cgr036Tests.ConnectAsync_ClosedPort_CallsOpenWithTheAddressThenCloseOnceAndFailsWithCouldntConnect` passes on every fast run of `Curl.Engine.UnitTests`, and `QueuedCallback.WaitUntilRun`'s lines are covered on every coverage run.

## Context

Found by BL-3657's lane on 2026-10-26: in one `dotnet test Curl.Engine.UnitTests --filter "TestCategory!=Integration"` run (with other lanes building on the machine) the test failed after 100 ms; it passed alone and in the solution-wide fast run just before. The cause was not investigated; it likely waits on timing rather than on the event it needs (a closed loopback port's refusal).

In the same session, one `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` run reported `QueuedCallback.WaitUntilRun` (`Curl.Engine.UnitLibrary/MultiCallbackQueue.cs`, lines 178 and 179) uncovered, while the run before it covered them: the tests that reach those lines depend on timing too.

## Acceptance criteria

- [x] The test waits for the event it needs rather than on timing, and its assertions are unchanged in meaning.
- [x] A test in `MultiCallbackQueueTests.cs` covers `QueuedCallback.WaitUntilRun`'s path where the wait is cancelled after the callback was already taken to run (it waits for the run and answers `true`), deterministically.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Closed-port test: the port was freed after being read, so another process could take it and the connect succeed; it now holds a bound, non-listening socket for the whole test. WaitUntilRun test: the existing callback-cancels test could reach Wait after the run finished; the new test waits until the caller thread is blocked, then cancels, then runs the queue.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: e5c455b0 59abc360 779790c0.
- 2026-10-28: Doing -> Done. closed-port test holds its port; WaitUntilRun cancelled-after-taken path covered deterministically
