---
id: BL-3278
title: Finish the -Z host queue's abort bookkeeping before resuming a cancelled transfer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/ParallelHostQueue.cs, Curl.Console/ParallelRun.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/ParallelHostQueueTests.cs, Curl.Console.UnitTests/ParallelRunTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3278 — Finish the -Z host queue's abort bookkeeping before resuming a cancelled transfer

## Goal

When a `-Z` run aborts (`--fail-early`), every waiting or freed transfer leaves `ParallelHostQueue` with its host count given back, and no cancelled transfer's code runs while the queue's lock is held or its bookkeeping is half done.

## Context

Found by the shift-end review (ADR-0710) of 6b344d38b...af2f6a9bd, type-design-analyzer and silent-failure-hunter (code-reviewer saw it too, below its bar).

The diff dropped `RunContinuationsAsynchronously` from `WaitingTransfer.Turn` (`Curl.Console/ParallelHostQueue.cs` around line 218) so `StartReleased` starts transfers one at a time on its own thread. Two consequences:

1. In `WaitForTurnAsync`'s abort callback (around line 149-155), `transfer.Turn.TrySetCanceled` runs first, inside `lock (hosts)`, so the cancelled transfer's continuation runs inline before `Waiting.Remove` and the `Running` decrement. It is safe today only because that path stops at `writeGate`; any later change that reaches `Leave` from there re-enters the queue mid-update.
2. `TakeReleased` removes a transfer from `released` under the lock, but `TrySetResult` runs after the lock is released (line 121-124). An abort landing in between cancels the `Turn`, finds the transfer in neither `Waiting` nor `released`, and never decrements `Running`; the transfer throws before the `try`/`finally` that calls `Leave`, so the host keeps a count it never gives back.

Also worth fixing while here: `ParallelTransferQueue.BeforeEachWait` defaults to a no-op and only `CurlCommandRunner.NewParallelRun` wires it to `Hosts.StartReleased` (`CurlCommandRunner.cs:1274`); `ParallelRun` owns both queues and could wire it itself so no caller can build a run whose freed transfers never start.

## Acceptance criteria

- [x] The abort callback does its `Waiting`/`released`/`Running` bookkeeping before completing `Turn`, and completes `Turn` outside `lock (hosts)`.
- [x] A transfer taken by `TakeReleased` and then aborted before it starts gives its host count back (a unit test pins the host's count after such an abort).
- [x] `ParallelRun` connects `Queue.BeforeEachWait` to `Hosts.StartReleased` itself, or the hook becomes a required constructor argument.
- [x] The existing order tests (`ParallelHostQueueOrderTests`, `CurlCommandRunnerParallelOrderTests`) still pass; `dotnet build` is clean and the fast tests pass.

## Notes

- touches widened to ParallelRun.cs, CurlCommandRunner.cs and ParallelRunTests.cs (no other Doing task names them). The abort callback now does its bookkeeping under the lock and cancels Turn after it. A transfer taken by TakeReleased and aborted before its Turn is set gives its count back in StartReleased (GiveBackCountIfAborted); StartReleased takes an optional afterEachTake hook so a test can land that abort. ParallelRun builds Queue with BeforeEachWait wired to Hosts.StartReleased. Measure-CodeQuality Curl.Console: 0 failing members; Curl.Console.UnitTests 2868 passed.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 2d9aca23 a39fa834 55027aec a482d2ec.
- 2026-10-23: Doing -> Done. -Z host queue finishes abort bookkeeping before cancelling, returns counts of taken-then-aborted transfers, ParallelRun wires its own hook
