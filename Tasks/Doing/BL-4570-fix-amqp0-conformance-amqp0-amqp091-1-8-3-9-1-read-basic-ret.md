---
id: BL-4570
title: Fix Amqp0 conformance: Amqp0:AMQP091-1.8.3.9-1 (read basic.return and its content)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ClientFlow.cs, Curl.Extended.Protocol.Amqp0.UnitLibrary/Methods, Curl.Extended.Protocol.Amqp0.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4570 — Fix Amqp0 conformance: Amqp0:AMQP091-1.8.3.9-1 (read basic.return and its content)

## Goal

Curl's `amqp://` client handles basic.return as AMQP 0-9-1 requires, so the conformance case Publish_ReturnedByTheBroker_IsReadWithItsContentAndTheChannelClosesCleanly passes.

## Context

Found by BL-4465 (Amqp0 conformance part 2): case `Curl.Conformance.Extended.UnitTests/Amqp0/Messaging/Amqp0MessagingTests.cs`
Publish_ReturnedByTheBroker_IsReadWithItsContentAndTheChannelClosesCleanly, requirement `Amqp0:AMQP091-1.8.3.9-1` in `Documentation/Conformance/Extended/Amqp0-Messaging.md`. 60.50 (basic.return) is not in Amqp0MethodReader, so a basic.return and its content arriving after a publish, while the flow awaits channel.close-ok, ends the transfer with `curl: (8) Weird server reply: AMQP 503 Method 60.50 is not one this client knows.` and connection.close 503. Add an Amqp0BasicReturn record (reply-code, reply-text, exchange, routing-key) to the reader, and have Amqp0ClientFlow read the returned content (Amqp0ContentAssembler) and skip it, so channel.close and connection.close still go out with 200. The client publishes without mandatory, so a broker returns only unasked; decide (ADR) whether the transfer then reports it, and keep stdout empty.
Library: `Curl.Extended.Protocol.Amqp0.UnitLibrary` (its CLAUDE.md, ADR-1056, ADR-1060).

## Acceptance criteria

- [ ] The case Publish_ReturnedByTheBroker_IsReadWithItsContentAndTheChannelClosesCleanly passes as a plain case.
- [ ] A focused unit test in `Curl.Extended.Protocol.Amqp0.UnitTests` pins the fix (ADR-0482).
- [ ] The `Amqp0:AMQP091-1.8.3.9-1` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 482aeb1b d78c25d3 865591a2 e97c1b31.
