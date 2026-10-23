---
id: BL-3253
title: Release a host's -Z waiters one at a time, first waiter last, in ParallelHostQueue
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/ParallelHostQueue.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelTransferQueue.cs, Curl.Console.UnitTests/ParallelHostQueueTests.cs, Curl.Console.UnitTests/ParallelHostQueueOrderTests.cs, Curl.Console.UnitTests/ParallelTransferQueueTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3253 — Release a host's -Z waiters one at a time, first waiter last, in ParallelHostQueue

## Goal

When a host's first `-Z` transfer ends, `ParallelHostQueue` releases its waiters one at a time, the first waiter last (ADR-0768), and a freed `--parallel-max` slot starts the earliest-waiting transfer.

## Context

Split from BL-3189. Curl races today: `ParallelHostQueue.Leave` releases every waiter at once on the thread pool. Rule: at the host's first end, the first waiter starts behind every transfer that starts in that same step; the rest keep URL order. Released transfers start one after another, each sending its request before the next begins. Design: `Leave` queues freed transfers in `released`; `StartReleased` starts them in turn (TCS without `RunContinuationsAsynchronously`); `ParallelTransferQueue.BeforeEachWait` runs before each wait. The existing `ParallelHostQueueTests` call `Leave` and expect an immediate release; each needs a `queue.StartReleased()` after `Leave`. Wiring into `CurlCommandRunner` is BL-3254. Read ADR-0768 in `Documentation/Planning/Decisions`.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests/ParallelHostQueueOrderTests.cs`, with a fake `TimeProvider` and no sleeping, shows that when a host's first transfer ends its waiters are released in the ADR-0768 order (first waiter last), one at a time.
- [x] A test in `Curl.Console.UnitTests/ParallelTransferQueueTests.cs` shows the transfer started when a slot frees is the earliest-waiting one.
- [x] A withdrawn freed transfer gives its host count back, shown by a test in `ParallelHostQueueTests.cs`.
- [x] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "FullyQualifiedName~Parallel&TestCategory!=Integration"` passes with the existing `-Z` tests unchanged apart from the added `StartReleased()` calls.

## Notes

- Split from BL-3189 (over its cost cap).
- Continues the stashed work of BL-3189.
- The keep-alive measurement (BL-3189's first criterion) is done; see the notes recorded in BL-3189: the pending transfer goes last only when the host's first connection was closed; with keep-alive it reuses the connection and keeps its place.
- The stashed `Leave` moved the first waiter behind *every* waiter, so under `--parallel-max-host 1` it freed `/3` instead of `/2` and the existing `ParallelHostQueueTests` (and with them the full Curl.Console fast run) hung. Fixed to ADR-0768's rule: the first waiter goes behind only the transfers freed in that same step (`FreeWaiting`). With that, the full Curl.Console fast run ends in seconds: 2847 passed, no hang.
- Added `Curl.Console/CurlCommandRunner.cs` to touches (no Doing task on the shared branch names it): the one-line `run.Queue.BeforeEachWait = run.Hosts.StartReleased` wiring has to land with the queue change, because without it no freed `-Z` transfer would ever start in production. BL-3254 keeps the holding-fake-server order test (`CurlCommandRunnerParallelOrderTests.cs`, stashed work left uncommitted for it: 1 of its 3 rows passes, while the default and `--parallel-max 2` rows fail because `ScriptedResultHandler` answers `/echo` synchronously).
- Measure-CodeQuality.ps1 -Library Curl.Console: 0 failing members (run with BL-3254's failing test file set aside).
- Platform: the new tests use no paths, OS errors or certificates, and every awaited event is a completed task, so nothing depends on scheduling order.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: e910298d 9f090ed0 d86ddc33 8aba9703.
- 2026-10-23: Doing -> Done. ParallelHostQueue frees a host's waiters one at a time, first waiter behind those freed with it; Curl.Console fast run green, quality gate clean
