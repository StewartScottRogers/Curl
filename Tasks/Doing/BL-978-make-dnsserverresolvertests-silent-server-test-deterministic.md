---
id: BL-978
title: Make DnsServerResolverTests' silent-server test deterministic so it cannot fail on a slow Linux runner
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests/DnsServerResolverTests.cs]
requirement: none
created: 2026-09-29
completed:
---
# BL-978 — Make DnsServerResolverTests' silent-server test deterministic so it cannot fail on a slow Linux runner

## Goal

`ResolveWithFailureReasonAsync_OneSilentServer_TriesThreeRoundsDoublingTheWaitThenTimesOut` passes every time on every platform, so it never turns CI red and blocks the shift-end merge.

## Context

CI run 36638516861 (commit 6ef19740) failed on ubuntu-latest only:

    CollectionAssert.AreEqual failed. Element at index 1 do not match.
    Expected: 2000

The test (`Curl.Networking.UnitTests/DnsServerResolverTests.cs:144`, from BL-694) uses `ManualTimeProvider` and `RunOutTimeoutsAsync` to advance time. Nine of the ten CI runs before it passed this test, so the failure is a race: the helper advances the clock before the resolver has sent its next query and armed its next timer, so a send is stamped at the wrong time. The sibling tests using `RunOutTimeoutsAsync` (`ThreeSilentServers_GoesRoundTheListThreeTimes` and others) share the risk.

Fix the helper, not the expectations: advance time only once the resolver is waiting on its timer (for example, wait until the opener has recorded the expected send, or until the `ManualTimeProvider` has a pending timer), with no real-time sleeps.

## Acceptance criteria

- [ ] `RunOutTimeoutsAsync` (or its replacement) advances the clock only after the resolver has armed the timer it waits on; no `Task.Delay` with real time and no `Thread.Sleep`.
- [ ] The test's expected timestamps (0, 2000, 6000; 14000) are unchanged.
- [ ] The whole `DnsServerResolverTests` class passes 200 times in a row locally (`dotnet test --filter FullyQualifiedName~DnsServerResolverTests` in a loop).
- [ ] The `CI` workflow passes on Windows, Linux and macOS for the commit that lands this.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
