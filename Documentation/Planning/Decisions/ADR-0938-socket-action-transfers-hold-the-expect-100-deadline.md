# ADR-0938: A socket-action transfer holds the expect-100 deadline through hooks on the HTTP handler

- Status: Accepted
- Date: 2026-10-28
- Decision: Decided by Claude under Stewart's delegation (BL-3864)
- Relates to: [ADR-0917](ADR-0917-socket-action-transfers-dial-through-a-gated-dialer.md), [ADR-0914](ADR-0914-socket-action-transfers-wait-for-readiness-beneath-tls.md)

## Context

ADR-0917 put a socket-action transfer's `TransferSocketWait` on `ManagedEngineSharedState.SocketWait`, but its
expect-100 deadline (`BeginExpectContinueWait`, `EndExpectContinueWait`) was not fed: the wait for `100 Continue`
is `HttpContinueWaitConnection.WaitForContinueAsync` in `Curl.Original.Protocol.Http.UnitLibrary`, which cannot reference
the engine. BL-3864 suggested an observer on `HttpRequestOptions`, but that record lives in
`Curl.Protocol.Abstractions.UnitLibrary` and is built per URL by `TransferContextFactory`, which never sees the
transfer's wait, while `ManagedCurlEngine.CreateDispatcher` builds the HTTP handler per perform from the state that
carries it.

## Decision

- `HttpProtocolHandler` takes two optional hooks, `ContinueWaitBegins` (`Action<TimeSpan>`) and `ContinueWaitEnds`
  (`Action`), and gives them to each `HttpContinueWaitConnection` it builds (`WaitBegins`, `WaitEnds`). Delegates,
  not a new interface, so the HTTP library adds no type the engine must implement.
- `WaitForContinueAsync` tells `WaitBegins` the wait's length as it starts a timed wait, and `WaitEnds` when the
  wait ends however it ends (`100 Continue`, another reply, the wait running out, cancellation). A wait longer than
  `LongestTimedWait` (about 49.7 days), which runs without a timer, tells no beginning: its deadline would outlast
  any transfer, and adding it to the clock could overflow `DateTimeOffset` for `CURLOPT_EXPECT_100_TIMEOUT_MS`
  values near `long.MaxValue`.
- `EngineComposition.CreateProtocolHandlers` takes the transfer's `TransferSocketWait` (`socketWait`, `null` by
  default) and sets the hooks to its `BeginExpectContinueWait` and `EndExpectContinueWait`
  (`ContinueWaitBeginsOf`, `ContinueWaitEndsOf`); `ManagedCurlEngine.CreateDispatcher` passes
  `ManagedEngineSharedState.SocketWait`. Outside socket-action mode both hooks are `null` and nothing changes.

## Consequences

While a socket-action transfer waits for `100 Continue`, `TransferSocketWait.EarliestDeadline` reports the
expect-100 timeout, so the multi handle can tell the application's timer callback of it; after the wait it is gone.
No option, output or exit code changes.
