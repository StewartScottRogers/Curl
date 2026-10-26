---
id: BL-3864
title: Feed a socket-action transfer's expect-100 wait to its TransferSocketWait
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3859]
touches: [Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestOptions.cs, Curl.Protocol.Http.UnitTests/HttpContinueWaitConnectionTests.cs, Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitTests/SocketActionGatedTcpDialerTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3864 — Feed a socket-action transfer's expect-100 wait to its TransferSocketWait

## Goal

While a transfer a multi handle drives by `curl_multi_socket_action` waits for `100 Continue`, its `TransferSocketWait` holds the expect-100 deadline (`BeginExpectContinueWait` with `CURLOPT_EXPECT_100_TIMEOUT_MS`, `EndExpectContinueWait` when the answer comes or the body goes without it), so `EarliestDeadline` reports it; outside socket-action mode nothing changes.

## Context

BL-3859 (ADR-0822) routed `SocketActionGatedTcpDialer` and carries the wait on `ManagedEngineSharedState.SocketWait`, but left the expect-100 hook out because it lands in `Curl.Protocol.Http.UnitLibrary`. The wait is `HttpContinueWaitConnection.WaitForContinueAsync` (built in `HttpProtocolHandler.cs` near `new HttpContinueWaitConnection(timedConnection) { ContinueWait = plan.Options.ContinueWait }`). The HTTP library cannot reference the engine, so add an optional observer on `HttpRequestOptions` (e.g. an `Action<TimeSpan>` begin and `Action` end, or a small interface in the HTTP library) that `HttpRequestOptionsMapping.Of` (or `ManagedCurlEngine.CreateDispatcher`) sets from the transfer's `SocketWait`.

## Acceptance criteria

- [ ] A unit test in `Curl.Protocol.Http.UnitTests` shows the observer told the continue wait's begin with its timeout and its end, both when `100 Continue` comes and when the wait times out.
- [ ] A unit test in `Curl.Engine.UnitTests` shows a socket-action transfer with `Expect: 100-continue` has `EarliestDeadline` at the expect-100 timeout while it waits, and none after.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and Measure-CodeQuality.ps1 for Curl.Protocol.Http.UnitLibrary and Curl.Engine.UnitLibrary shows no member outside a gate.

## Notes

- Split from BL-3859 by its run: the hook needs `Curl.Protocol.Http.UnitLibrary`, outside BL-3859's touches. The engine file that sets the observer may be `ManagedCurlEngine.cs` instead of `HttpRequestOptionsMapping.cs`; widen touches if so.

## Log
- 2026-10-26: Created.
