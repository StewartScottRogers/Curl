---
id: BL-3864
title: Feed a socket-action transfer's expect-100 wait to its TransferSocketWait
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3859]
touches: [Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestOptions.cs, Curl.Protocol.Http.UnitTests/HttpContinueWaitConnectionTests.cs, Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitTests/SocketActionGatedTcpDialerTests.cs, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3864 — Feed a socket-action transfer's expect-100 wait to its TransferSocketWait

## Goal

While a transfer a multi handle drives by `curl_multi_socket_action` waits for `100 Continue`, its `TransferSocketWait` holds the expect-100 deadline (`BeginExpectContinueWait` with `CURLOPT_EXPECT_100_TIMEOUT_MS`, `EndExpectContinueWait` when the answer comes or the body goes without it), so `EarliestDeadline` reports it; outside socket-action mode nothing changes.

## Context

BL-3859 (ADR-0917) routed `SocketActionGatedTcpDialer` and carries the wait on `ManagedEngineSharedState.SocketWait`, but left the expect-100 hook out because it lands in `Curl.Protocol.Http.UnitLibrary`. The wait is `HttpContinueWaitConnection.WaitForContinueAsync` (built in `HttpProtocolHandler.cs` near `new HttpContinueWaitConnection(timedConnection) { ContinueWait = plan.Options.ContinueWait }`). The HTTP library cannot reference the engine, so add an optional observer on `HttpRequestOptions` (e.g. an `Action<TimeSpan>` begin and `Action` end, or a small interface in the HTTP library) that `HttpRequestOptionsMapping.Of` (or `ManagedCurlEngine.CreateDispatcher`) sets from the transfer's `SocketWait`.

## Acceptance criteria

- [x] A unit test in `Curl.Protocol.Http.UnitTests` shows the observer told the continue wait's begin with its timeout and its end, both when `100 Continue` comes and when the wait times out.
- [x] A unit test in `Curl.Engine.UnitTests` shows a socket-action transfer with `Expect: 100-continue` has `EarliestDeadline` at the expect-100 timeout while it waits, and none after.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and Measure-CodeQuality.ps1 for Curl.Protocol.Http.UnitLibrary and Curl.Engine.UnitLibrary shows no member outside a gate.

## Notes

- Split from BL-3859 by its run: the hook needs `Curl.Protocol.Http.UnitLibrary`, outside BL-3859's touches. The engine file that sets the observer may be `ManagedCurlEngine.cs` instead of `HttpRequestOptionsMapping.cs`; widen touches if so.

- Touches widened to `Curl.Engine.UnitLibrary/EngineComposition.cs` and `ManagedCurlEngine.cs` (no Doing task on origin/work/dark-factory named either): the HTTP handler is built per perform in `CreateProtocolHandlers`, which `CreateDispatcher` calls with the state carrying `SocketWait`. `HttpRequestOptions` lives in `Curl.Protocol.Abstractions` and is built by `TransferContextFactory`, which never sees the wait, so the observer went on `HttpProtocolHandler` instead (ADR-0937). `HttpRequestOptions.cs` and `HttpRequestOptionsMapping.cs` stay unchanged.
- Decision: the hooks are two delegates (`ContinueWaitBegins` `Action<TimeSpan>`, `ContinueWaitEnds` `Action`), not a new interface, so the HTTP library adds no type the engine must implement; a wait longer than `LongestTimedWait` tells no beginning (its deadline outlasts any transfer and could overflow `DateTimeOffset`), but still tells its end.
- Measured: Measure-CodeQuality.ps1 for Curl.Protocol.Http.UnitLibrary shows 0 failing members (after splitting `WaitForContinueAsync`, which first reached complexity 12). For Curl.Engine.UnitLibrary the one failing member is `QueuedCallback.WaitUntilRun` (MultiCallbackQueue.cs lines 178-179, timing-dependent coverage in code this task does not touch), which BL-3908 already covers; the new `ContinueWaitBeginsOf`/`ContinueWaitEndsOf` are fully covered.
- The new tests use no paths, OS errors or certificates, and the engine test waits on `TransferSocketWait.Changed` before reading the wait, so it does not depend on the platform.

## Log
- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 4700c00b a15d924b c3428d92.
- 2026-10-28: Doing -> Done. A socket-action transfer's TransferSocketWait holds the expect-100 deadline while it waits for 100 Continue
