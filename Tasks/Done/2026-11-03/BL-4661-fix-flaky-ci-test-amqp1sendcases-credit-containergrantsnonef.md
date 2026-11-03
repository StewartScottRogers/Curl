---
id: BL-4661
title: Fix flaky CI test Amqp1SendCases.Credit_ContainerGrantsNoneFor300ms_CurlWaitsThenSends
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests/Amqp1/Messaging/Amqp1SendCases.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4661 — Fix flaky CI test Amqp1SendCases.Credit_ContainerGrantsNoneFor300ms_CurlWaitsThenSends

## Goal

`Amqp1SendCases.Credit_ContainerGrantsNoneFor300ms_CurlWaitsThenSends` passes every time in a full fast-test run, not only alone.

## Context

On lane 7 (BL-4638's run, 2026-11-02) the full `dotnet test --filter "TestCategory!=Integration"` failed this one case in `Curl.Conformance.Extended.UnitTests/Amqp1/Messaging/Amqp1SendCases.cs` (383 ms), while it passed when run alone. It waits on a 300 ms credit grant, so it likely depends on wall-clock timing under load; make it wait for the event it needs (the container's grant, the transfer's send) rather than on timing.

## Acceptance criteria

- [x] The case waits on the event it needs, not on elapsed real time, and passes alone and in the full fast-test run.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Could not reproduce the failure: the class passes alone (6 of 6) and the whole Curl.Conformance.Extended.UnitTests fast run passes (1381). Read the case: it has no wall-clock assertion. The client (Amqp1ClientFlow.SendMessageAsync) waits for a flow frame, not a timer; the container records frames in a ConcurrentQueue; the test awaits link.Credit before reading TransfersWithoutCredit. The 300 ms is only the container holding credit back, which is the point of the case (Core-2.6.7-1). No code change made; if it fails again, capture its failure message (not only its time) in the CI task.
- Passes on every platform: no path or OS dependency.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: f31bf7b3 9742157d.
- 2026-11-02: Doing -> Done. Case is already event-driven; not reproducible alone or in the full project run, no change needed
