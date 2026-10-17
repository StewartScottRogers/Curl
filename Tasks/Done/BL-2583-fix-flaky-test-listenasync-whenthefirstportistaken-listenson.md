---
id: BL-2583
title: Fix flaky test ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne in Curl.Networking.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2583 — Fix flaky test ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne in Curl.Networking.UnitTests

## Goal

`ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne` passes every time, whether it runs alone or in the full fast test run while other lanes run tests on the same machine.

## Context

On 2026-10-17 BL-2569's lane-2 run saw it fail in the full fast run (`dotnet test --filter "TestCategory!=Integration"`, Curl.Networking.UnitTests: 1 failed, 3229 passed, 12 ms) and pass when run alone straight after. The likely cause is a fixed or guessed port that another process (another lane's test run) can take between the test choosing it and listening on it. Start from the test in `Curl.Networking.UnitTests` and the `ListenAsync` it drives in `Curl.Networking.UnitLibrary`.

## Acceptance criteria

- [x] The test no longer depends on a port another process can take: it takes its ports from the OS (port 0) or holds them for the whole test, as its Notes explain.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause was not provable from one failure; the test guessed the range `heldPort..heldPort+64` clamped to 65535, which leaves no room when the OS hands out a port near the top, and nothing absorbed a crowded neighbourhood. The held port still comes from the OS (port 0), is held for the whole attempt, and the test now takes another OS-chosen port (up to 10 tries) when the held one is above 65471 or its range had no free port. Production code unchanged.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: c4e1aef4 9742157d.
- 2026-10-17: Doing -> Done. Test takes its port from the OS and retries on a crowded neighbourhood
