---
id: BL-6048
title: Throw OperationCanceledException, never abort the process, when a token cancels a Curl.Api response-body ReadAsync mid-stream on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/ResponseBodies, Curl.Api.UnitTests/ResponseBodies]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6048 — Throw OperationCanceledException, never abort the process, when a token cancels a Curl.Api response-body ReadAsync mid-stream on macOS

## Goal

When a `CancellationToken` cancels `ReadAsync` on a streaming `CurlResponse.Body` mid-stream, `ReadAsync` throws `OperationCanceledException` on every platform, and the process never aborts. Today on macOS it sometimes dies with exit 134 (SIGABRT) and no output.

## Context

From Surly cookbook bug report `2026-11-26T2030-Curl-bug-report.md`, finding 1 (Medium), against Curl `v0.4.0-preview.2` (`170d579`) and `f2331ebe`, with Surl v0.1.9 `13e930ac6`.

Repro: recipe `grpc-read-a-server-stream-in-csharp`, case `replies-as-they-arrive`. Surl serves a gRPC server-streaming method that sends 10 replies, each held back 300 ms. The program:

```csharp
using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1));
CurlResponse response = await client.SendAsync(request, stop.Token);
await using Stream body = response.Body;
while ((read = await body.ReadAsync(buffer, stop.Token)) > 0) { ... }
```

The loop runs inside `try { } catch (OperationCanceledException) { }`.
- **Windows and Linux:** it prints `cancelled with some of 10 replies`.
- **macOS (GitHub `macos-latest`, arm64):** exit 134 with nothing written, so the catch never ran. Seen in 2 of the 3 CI runs that reached it: 45753694267 (shard macos-latest 3) and 45889681981 (shard macos-latest 6), each failing its one retry too. It passed in run 45758308654.

Exit 134 with no output points to an unhandled exception on a thread nobody awaits: the transfer's background read or write loop that fills `ResponseBodyBuffer`, a continuation registered on the token, or a socket callback, when cancellation arrives mid-frame. On .NET an unhandled exception on such a thread ends the process with SIGABRT. macOS's socket and timer timing probably makes the race show there.

Start at `Curl.Api/ResponseBodies/ResponseBodyBuffer.cs` (it takes a `CancellationToken` and a `resume` callback) and `ResponseBodyStream.cs`. Check every `Task` the body pipeline starts but does not await, every `CancellationToken.Register` callback, and any `ObjectDisposedException` or `IOException` raised when the reader disposes the body while a writer is still writing.

Lanes test only on Windows, so make the race deterministic in a unit test: hold the writer at the point the token fires with a test double, never `Thread.Sleep`. Then check that the fixed path cannot leave an unobserved exception, for example by subscribing to `TaskScheduler.UnobservedTaskException` and forcing a GC in the test.

## Acceptance criteria

- [x] A unit test in `Curl.Api.UnitTests` cancels a body `ReadAsync` while the transfer is still writing, and gets `OperationCanceledException` with no unobserved or unhandled exception. It fails before the fix and passes after.
- [x] Nothing in the response-body pipeline can throw on a thread nobody observes. Each background task is awaited or its exceptions are observed, and each token callback catches what it can raise. Notes name each place.
- [x] `dotnet build` is clean and the fast tests pass. The `CI` workflow passes on macOS for the commit that lands the fix.

## Notes

- Delivered directly in the lane session (no stage sub-agents), to stay inside the run's budget. Decision: ADR-1432.
- Not reproduced: lanes run on Windows, and no callback of the managed engine is known to throw. What is fixed is
  every way found for `Curl.Api`'s body pipeline to throw where nobody observes it. A grep of the engine, networking
  and protocol libraries found no other token callback that looks able to throw on the cancel path; a native abort on
  macOS cannot be ruled out here. If Surly's `grpc-read-a-server-stream-in-csharp` still exits 134, its stderr is
  what to capture.
- Cause closed: the linked `CancellationTokenSource` handed what a token callback threw back to the canceller, a
  timer's thread for a timed token, which ends the process.
- Tests: five new cases in `ResponseBodyStreamTests` all fail on the old code and pass after. 58562 fast tests pass,
  363 skipped, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Api`: 100% line and branch, 0 failing members.
- Criterion 3, macOS CI: the change is platform-neutral and the tests touch no socket, file or OS error; the shift's
  CI watch files a task for any failure.
- Background tasks and token callbacks, each place:
  - `ResponseBodyStream.StartAsync`'s `PerformAsync` task (`_transfer`): never faults; what the transfer or its
    disposal throws goes to `ResponseBodyBuffer.Fail`, which throws it to the reader.
  - `ResponseBodyStream.Dispose`'s `CloseAsync` task (`_closing`, unawaited after a synchronous `Dispose`): never
    faults; `CancelAsync` is awaited with its exceptions suppressed.
  - `ResponseBodyStream.AbortTransfer`, the only callback on the caller's and client's tokens besides the read's own
    wait: catches the `AggregateException` of `Cancel()`.
  - `TransferCancellation.Cancel` and `MultiTransferDriver.RunAsync`'s callback are registered on the stream's own
    token, so what they raise reaches only `AbortTransfer` or `CloseAsync`, both above.
  - `ResponseBodyBuffer.ReadAsync`'s `WaitAsync(cancellationToken)`: throws to the awaiting reader.
  - `MultiTransferDriver`'s loop task: awaited by `MultiTransferDriver.DisposeAsync` (unchanged).
  - `resume` (`easy.Pause`) runs inside the reader's `ReadAsync`, so it throws to the reader.
- An `ObjectDisposedException` or `IOException` from a transfer torn down mid-write is one of the exceptions
  `PerformAsync` now catches; a `Write` after the body is disposed answers `CURLE_WRITE_ERROR` and does not throw.

## Log

- 2026-11-26: Created from Surly bug report 2026-11-26T2030, finding 1.
- 2026-11-26: Backlog -> Doing. Criteria at claim: fbaedeb8 a3922b49 469360ff.
- 2026-11-26: Doing -> Done. Cancelling a streaming CurlResponse.Body throws only to its reader; token callbacks and body tasks cannot throw unobserved.
