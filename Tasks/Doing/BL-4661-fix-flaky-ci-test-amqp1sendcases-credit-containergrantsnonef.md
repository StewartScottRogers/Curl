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
completed:
---
# BL-4661 — Fix flaky CI test Amqp1SendCases.Credit_ContainerGrantsNoneFor300ms_CurlWaitsThenSends

## Goal

`Amqp1SendCases.Credit_ContainerGrantsNoneFor300ms_CurlWaitsThenSends` passes every time in a full fast-test run, not only alone.

## Context

On lane 7 (BL-4638's run, 2026-11-02) the full `dotnet test --filter "TestCategory!=Integration"` failed this one case in `Curl.Conformance.Extended.UnitTests/Amqp1/Messaging/Amqp1SendCases.cs` (383 ms), while it passed when run alone. It waits on a 300 ms credit grant, so it likely depends on wall-clock timing under load; make it wait for the event it needs (the container's grant, the transfer's send) rather than on timing.

## Acceptance criteria

- [ ] The case waits on the event it needs, not on elapsed real time, and passes alone and in the full fast-test run.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: f31bf7b3 9742157d.
