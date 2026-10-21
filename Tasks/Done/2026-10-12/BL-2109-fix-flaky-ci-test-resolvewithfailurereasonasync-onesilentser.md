---
id: BL-2109
title: Fix flaky CI test ResolveWithFailureReasonAsync_OneSilentServer_TriesThreeRoundsDoublingTheWaitThenTimesOut on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-12
completed: 2026-10-12
---
# BL-2109 — Fix flaky CI test ResolveWithFailureReasonAsync_OneSilentServer_TriesThreeRoundsDoublingTheWaitThenTimesOut on Windows

## Goal

`DnsServerResolverTests.ResolveWithFailureReasonAsync_OneSilentServer_TriesThreeRoundsDoublingTheWaitThenTimesOut`
passes on every run, whatever the CI machine's speed.

## Context

CI run 38245611636 (push of b3a5e8021 on `audit`, Windows, 2026-10-12) failed it after 16 s:
"Assertion failed. Expected value to be less than the upper bound. The lookup neither finished
nor waited. upper bound: 10000, actual: 10966". The same commit's pull_request run passed, so
the failure is timing, not code. The test lives in `Curl.Networking.UnitTests/DnsServerResolverTests.cs`.
Its 10000 ms bound is measured against real elapsed time or a spin loop. CLAUDE.md says to inject
`TimeProvider` for anything time-dependent, so drive the rounds with a fake `TimeProvider` and
assert on the waits the resolver asked for, not on wall-clock time.

## Acceptance criteria

- [x] The test asserts on the resolver's requested waits through an injected fake `TimeProvider`, with no wall-clock bound.
- [x] The test passes 50 runs in a row (`dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~OneSilentServer_TriesThreeRounds"`, repeated).
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- Cause: the test already drove the rounds with a fake `TimeProvider` (`TimerCountingTimeProvider`) and asserted the send times and clock on it; the 10000 ms bound was a hang guard in `RunOutTimeoutsAsync`, a `Task.Yield` spin loop timed by a `Stopwatch`. Under a loaded CI runner (method-level parallel tests, every spinner hogging the thread pool) the spin itself ran past 10 s.
- Fix: the helper now waits on a `SemaphoreSlim` released by two new test-fake events (`TimerCountingTimeProvider.TimerCreated`, `ScriptedDnsSocketOpener.QueryRecorded`) or on the lookup ending - no spin, no wall clock. Choice: no replacement hang guard; a hung lookup now hangs the test rather than failing it, which the CI job timeout catches; any wall-clock guard would reintroduce a machine-speed dependency. No production code changed, so Curl.Networking.UnitLibrary is untouched.
- Verified: 50 consecutive runs of the filtered test passed; full build with -warnaserror clean; fast tests green.

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing.
- 2026-10-12: Doing -> Done. Test helper waits on timer/send events instead of a wall-clock spin; 50 runs green
