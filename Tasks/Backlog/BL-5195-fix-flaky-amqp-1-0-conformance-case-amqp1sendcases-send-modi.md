---
id: BL-5195
title: Fix flaky AMQP 1.0 conformance case Amqp1SendCases.Send_Modified_FailsWithExit55
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests/Amqp1/Messaging/Amqp1SendCases.cs]
requirement: none
created: 2026-11-14
completed:
---
# BL-5195 — Fix flaky AMQP 1.0 conformance case Amqp1SendCases.Send_Modified_FailsWithExit55

## Goal

Amqp1SendCases.Send_Modified_FailsWithExit55 passes reliably on a loaded CI runner: the reference broker's credit grant is not cancelled before it is written.

## Context

Failed once on Linux in CI run 43578396101 (job 132397650633, PR #336 head 8a8b41ced, 2026-11-14 02:14 Arizona) after 22 ms, and passed on the rerun of the same commit and in push run 43578396704, so it is flaky, not broken.

Failure: `System.Threading.Tasks.TaskCanceledException: A task was canceled.` thrown from
`ReferenceConnection.WriteAsync` (`Curl.Conformance.Extended.UnitLibrary/Harness/ReferenceConnection.cs:35`) <- `Amqp1ReferenceReceivingLink.GrantCreditAsync` (`Curl.Conformance.Extended.UnitLibrary/Amqp1/Messaging/Amqp1ReferenceReceivingLink.cs:55`) <- `Amqp1SendCases.SendAsync(outcome, maxFrameSize, body, creditDelayMilliseconds)` (`Curl.Conformance.Extended.UnitTests/Amqp1/Messaging/Amqp1SendCases.cs:30`) <- the `Send_Modified_FailsWithExit55` case (:102), run through `KnownFailures.RunCaseAsync`.

So the reference broker tried to grant link credit after the connection's token was already cancelled - most likely Curl had already seen the `modified` outcome path end the exchange (exit 55) and closed, a race between Curl's close and the broker's credit write, or the `creditDelayMilliseconds` wall-clock delay. Make the reference side tolerate a peer that has already closed (or order the case so credit is granted before Curl can finish), without weakening what the case asserts (exit 55 for a `modified` outcome). If the fix belongs in the harness library, add that file to `touches` before editing it.

## Acceptance criteria

- [ ] The race is named in the Log.
- [ ] The case no longer fails when Curl closes the connection before the reference broker's credit write, and it still asserts exit 55 for the `modified` outcome.
- [ ] `dotnet test Curl.Conformance.Extended.UnitTests --filter "FullyQualifiedName~Amqp1SendCases"` passes 20 runs in a row.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
