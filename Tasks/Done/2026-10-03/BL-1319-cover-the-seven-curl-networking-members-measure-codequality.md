---
id: BL-1319
title: Cover the seven Curl.Networking members Measure-CodeQuality reports below 100% branch or above complexity 10
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1319 — Cover the seven Curl.Networking members Measure-CodeQuality reports below 100% branch or above complexity 10

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Context

Found by BL-1284 on 2026-10-03, in code BL-1284 did not change: `AddressFamilyRace.DialAsync` (branch 88.89%, Cx 18), `TcpConnector.DialAndOpenThroughProxyAsync` (91.67%, Cx 12), `TcpConnector.RequestTunnelAsync` (70%), `TcpConnector.ConnectWithinTimeoutAsync` (83.33%), `ConnectTunnelVerboseLines.ReportBeforeConnect` (75%), `TcpConnector.SecureOpenedTunnelAsync` (50%), `PooledConnection.IsSharedWithAnotherTransfer` (50%).

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for every member.

## Notes

- Measured 2026-10-03 after the change: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 0 failing members.
- The tunnel's `HttpProxyTunnelTrace` is made on both tunnel paths (plain and over TLS), so its `?.` null branches in `RequestTunnelAsync` and `SecureOpenedTunnelAsync` could never run: the trace is now a non-null parameter of `OpenTunnelAsync`, `RequestTunnelAsync` and `SecureOpenedTunnelAsync`, and `TunnelRequest.Trace` is gone. No behaviour change.
- `AddressFamilyRace.DialAsync` (Cx 18): the loop moved to `RaceAsync`, a failed attempt's follow-up to `StartAfterFailure`, and the `await`s in `finally` were replaced by `RaceCapturingExceptionAsync` (the compiler's rethrow in an async `finally` has a branch C# cannot reach), as `RequestTunnelAsync` already captures with `ExceptionDispatchInfo`.
- `DialAndOpenThroughProxyAsync` (Cx 12): the dial failure moved to `ProxyDialFailure`, the per-kind tunnel to `OpenThroughProxyKindAsync`, its switch written as `if`s (the switch's compiled form left one branch uncovered with every kind tested). `ConnectWithinTimeoutAsync`'s tuple switch is likewise two `if`s.
- New tests: `ReportBeforeConnect_WithASentValueAndNoCredential_NamesAnEmptyUser`, `IsSharedWithAnotherTransfer_AfterItsLeaseEnded_KeepsWhatItWasWhenItEnded`.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Measure-CodeQuality reports no failing member in Curl.Networking.UnitLibrary
