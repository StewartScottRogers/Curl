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
completed:
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

- [ ] The test asserts on the resolver's requested waits through an injected fake `TimeProvider`, with no wall-clock bound.
- [ ] The test passes 50 runs in a row (`dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~OneSilentServer_TriesThreeRounds"`, repeated).
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing.
