---
id: BL-4547
title: Fix flaky CI test Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-4543]
touches: [Curl.Conformance.Extended.UnitTests/Mqtt5/Mqtt5ConnectTests.cs, Curl.Conformance.Extended.UnitLibrary/Harness/ReferenceRun.cs]
requirement: none
created: 2026-11-02
completed:
---
# BL-4547 — Fix flaky CI test Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst

## Goal

`Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` passes every time in the full fast-test run of `Curl.Conformance.Extended.UnitTests`, not only alone.

## Context

Found by BL-4521 on lane 3 (2026-11-02): it failed once (643 ms) in `dotnet test Curl.slnx --filter "TestCategory!=Integration"` with 230 other tests of the project passing, and passed when run alone. Likely a race under method-level parallelism: the test must wait for the event it reads (the CONNECT read by the reference server) rather than rely on scheduling (lane rules, AF-0226 re-audit). Start at `Curl.Conformance.Extended.UnitTests/Mqtt5/Mqtt5ConnectTests.cs`.

## Acceptance criteria

- [ ] The test waits for the event it asserts on and holds no state shared with other tests; the cause is named under Notes.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-11-02 (lane 3, before any edit): likely cause, from reading the code. `ExtendedConformanceHarness.RunAsync` calls `ReferenceRun.EndAsync` as soon as curl returns, and `EndAsync` cancels `Stopping` straight away. The broker's server task (`ReferenceTcpDialer.ServeAsync`, which starts with `Task.Yield`) may not yet have read the PUBLISH and DISCONNECT curl left buffered in the in-memory pair, so cancelling cuts its read loop short and `broker.ReceivedPackets` (or `ClientBytes`) misses packets. Under method-level parallelism the thread pool is busy, so the server lags behind curl. The fix belongs in the harness (`Curl.Conformance.Extended.UnitLibrary/Harness/ReferenceRun.cs`): let each server read its connection to EOF, since curl has closed it, before cancelling the servers still waiting. That file is outside this task's touches, so it is added above.
- 2026-11-02: requeued without editing. BL-4543 (the CI fix for this same test, holding all of Curl.Conformance.Extended.UnitTests and .UnitLibrary) and BL-4544 (the same flake, holding Mqtt5ConnectTests.cs) are both in Doing on work/dark-factory. This task now depends on BL-4543. Once that is Done, the next claim only has to check that the test waits for what it asserts on and that the fast tests are green, then close this task, or defer it as a duplicate if BL-4543's fix already covers it.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 6d1426b8 59888c86.
- 2026-11-02: Doing -> Backlog. Waits on BL-4543 (CI fix for the same test, holding Curl.Conformance.Extended.UnitTests and .UnitLibrary incl. Harness/ReferenceRun.cs)
- 2026-11-02: Backlog -> Doing. Criteria at claim: 6d1426b8 59888c86.
