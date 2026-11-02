---
id: BL-4500
title: Let CurlComposition's dialing runner take an injected TimeProvider so -m and --connect-timeout run on it
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Console/InProcessCurl.cs, Curl.Console.UnitTests/CurlCompositionInjectedTimeProviderTests.cs, Curl.Console.UnitTests/InProcessCurlTests.cs]
requirement: none
created: 2026-11-02
completed:
---
# BL-4500 — Let CurlComposition's dialing runner take an injected TimeProvider so -m and --connect-timeout run on it

## Goal

`InProcessCurl` and the dialing `CurlComposition.CreateRunner` take an optional `TimeProvider` that the run's `TransferPerformer`, `ConnectionCache`, TLS session cache and transfer timers all use, so a test can drive `-m` and `--connect-timeout` with a fake clock and get exit 28 without waiting.

## Context

Split from BL-4462 (the extended-protocol conformance harness), whose criterion "a `-m` timeout on an injected `TimeProvider` (exit 28)" needs it. Today the dialing `CreateRunner` in `Curl.Console/CurlComposition.cs` (about line 368) builds `new ConnectionCache(TimeProvider.System)` and `new TransferPerformer(TimeProvider.System, EasyErrorText)`, and `InProcessCurl.RunAsync` has no clock parameter; `signingClock` covers only signing. Add a `TimeProvider? timeProvider = null` parameter (null meaning `TimeProvider.System`) and a public `InProcessCurl.RunAsync` overload that takes it beside `readEnvironmentVariable`.

## Acceptance criteria

- [x] The dialing `CurlComposition.CreateRunner` and a public `InProcessCurl.RunAsync` overload take an optional `TimeProvider`, used for every timer of the run in place of `TimeProvider.System`.
- [x] A test in `Curl.Console.UnitTests/CurlCompositionInjectedTimeProviderTests.cs` runs `-m 1` against an in-memory server that never answers, advances a fake `TimeProvider`, and gets exit 28 with no real socket and no real wait.
- [x] `dotnet build` is clean and the fast tests are green; `Curl.Console` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes
- The dialing `CurlComposition.CreateRunner` takes `TimeProvider? timeProvider = null` (last parameter; `RunClockOf` gives `TimeProvider.System` for null) and runs the run's `ConnectionCache`, the runner itself, the `TransferPerformer` (whose `-m`, `-y` and connect watchdogs it starts), each group's TLS providers (and their session caches), TCP and pooling connectors, and the DoH connector (`DialingDohResolverOf` now takes the clock) on it. `signingClock` stays separate, as before. `InProcessCurl.RunAsync` gains a public overload taking `readEnvironmentVariable` and a `TimeProvider`.
- Widened `touches` to `Curl.Console.UnitTests/InProcessCurlTests.cs`: its null-listener guard test became ambiguous between the two 9-argument overloads, so it now casts `(IConnectionListener)null!`. No task in Doing on origin/work/dark-factory names that file.
- The test runs `-m 1`, as the criterion says, and `-m 100`, advancing its fake clock by the `-m` time each; the `-m 100` row can only pass on the injected clock: with the system clock it would take 100 s and fail on the test's 30 s wait (it runs in about 0.4 s).
- Quality: `CreateRunner`'s complexity reached 12, so the `CURL_DNS_SERVER` ternary and the resolver-hook default moved into `HonouredDnsServerOf` and `ResolverHookActivatorsOf` beside `RunClockOf`. `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Full fast tests: 114 test projects passed, none failed.
- CI on Linux and macOS: the change and test are platform-neutral (no paths, OS text or certificates; the test waits on the read event before it advances the clock).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 95e71a5c 595af6b4 f3752ca2.
