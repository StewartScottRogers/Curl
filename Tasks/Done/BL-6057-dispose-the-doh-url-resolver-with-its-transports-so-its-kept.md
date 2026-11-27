---
id: BL-6057
title: Dispose the --doh-url resolver with its transports so its kept connections close
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/CurlTransports.cs, Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Engine.UnitTests/EngineCompositionDohTests.cs, Curl.Console/CurlComposition.cs, Curl.Engine.UnitLibrary/TransferDispatch.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Console.UnitTests/CurlCommandRunnerDialingDohTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6057 — Dispose the --doh-url resolver with its transports so its kept connections close

## Goal

Whoever builds the transports disposes the `--doh-url` resolver when their transfers are done, so its kept connections close.

## Context

ADR-1441 (BL-6051): `DohDnsResolver` keeps its DoH connections for later queries and closes them in `DisposeAsync`, which nothing calls, so they stay open until the process ends. That matters most to a long-lived `Curl.Api` or engine host. Start at `EngineComposition.CreateDnsResolver` and `CurlTransports`; follow who owns and disposes the transports.

## Acceptance criteria

- [x] A unit test shows that disposing the transports (or their owner) closes a connection the DoH resolver kept.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- ADR-1446: `CurlTransports` is `IAsyncDisposable` (it closes its `DohDnsResolver` only); `TransferDispatch.KeptDohConnections` closes it with the console's option group; each library perform disposes the transports it built.
- Added to `touches` (no other task in Doing on `origin/work/dark-factory` named them): `TransferDispatch.cs` (the console's owner of the transports), `ManagedCurlEngine.cs`, `ManagedEngineSharedState.cs`, `ManagedEasyTransfer.cs` and `ManagedEasyTransfer.Perform.cs` (the library host builds transports on every perform, the long-lived case Context names), and `CurlCommandRunnerDialingDohTests.cs`. `EngineComposition.Dns.cs` needed no change.
- Tests: `EngineCompositionDohTests` (`DisposeAsync_*`, `PerformAsync_WithADohUrl_ClosesTheConnectionsTheDohResolverKept`) and `CurlCommandRunnerDialingDohTests`.
- Checks: build with 0 warnings; the whole fast test run passed (exit 0). It found `PerformAsync` over the complexity gate, so the disposal moved to `PerformDisposingTransportsAsync`; the rebuild was clean and the Engine quality run, which reruns the test projects that reach the engine, passed at 100% line and branch with 0 failing members. `Curl.Console` was 100% with 0 failing members in the first quality run and did not change after it.
- The new tests are platform-neutral (in-memory dialer, plain `http` DoH URL).
- Follow-up: BL-6065, reuse a library handle's DoH connections across performs.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: c382bbb3 3d836d5e.
- 2026-11-26: Doing -> Done. The --doh-url resolver is disposed with the console's option group and after each library perform, so its kept connections close
