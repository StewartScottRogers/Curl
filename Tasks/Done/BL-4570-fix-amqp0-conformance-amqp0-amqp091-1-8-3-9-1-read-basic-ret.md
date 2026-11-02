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
completed: 2026-11-02
---
# BL-4570 — Fix Amqp0 conformance: Amqp0:AMQP091-1.8.3.9-1 (read basic.return and its content)

## Goal

Curl's `amqp://` client handles basic.return as AMQP 0-9-1 requires, so the conformance case Publish_ReturnedByTheBroker_IsReadWithItsContentAndTheChannelClosesCleanly passes.

## Context

Found by BL-4465 (Amqp0 conformance part 2): case `Curl.Conformance.Extended.UnitTests/Amqp0/Messaging/Amqp0MessagingTests.cs`
Publish_ReturnedByTheBroker_IsReadWithItsContentAndTheChannelClosesCleanly, requirement `Amqp0:AMQP091-1.8.3.9-1` in `Documentation/Conformance/Extended/Amqp0-Messaging.md`. 60.50 (basic.return) is not in Amqp0MethodReader, so a basic.return and its content arriving after a publish, while the flow awaits channel.close-ok, ends the transfer with `curl: (8) Weird server reply: AMQP 503 Method 60.50 is not one this client knows.` and connection.close 503. Add an Amqp0BasicReturn record (reply-code, reply-text, exchange, routing-key) to the reader, and have Amqp0ClientFlow read the returned content (Amqp0ContentAssembler) and skip it, so channel.close and connection.close still go out with 200. The client publishes without mandatory, so a broker returns only unasked; decide (ADR) whether the transfer then reports it, and keep stdout empty.
Library: `Curl.Extended.Protocol.Amqp0.UnitLibrary` (its CLAUDE.md, ADR-1056, ADR-1060).

## Acceptance criteria

- [x] The case Publish_ReturnedByTheBroker_IsReadWithItsContentAndTheChannelClosesCleanly passes as a plain case.
- [x] A focused unit test in `Curl.Extended.Protocol.Amqp0.UnitTests` pins the fix (ADR-0482).
- [x] The `Amqp0:AMQP091-1.8.3.9-1` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

- Delivered directly (a two-file fix): `Amqp0BasicReturn` (60.50) in `Amqp0MethodReader`; `Amqp0ClientFlow.ReceiveMethodAsync` reads any basic.return with its content and skips it, so the publish closes channel and connection with 200 and exits 0 with nothing written (ADR-1197, decided under delegation: the client never sets mandatory, so a return is unasked).
- Tests: `Amqp0MethodReaderTests` round-trips basic.return; `Amqp0ProtocolHandlerTests.ExecuteAsync_BasicReturnAfterPublish_SkipsTheReturnedMessageAndClosesWith200` pins the flow (ADR-0482). The conformance case passes as a plain case with its KnownFailures line (which named BL-4563) removed.
- `dotnet build Curl.slnx -warnaserror` clean, fast tests green, Measure-CodeQuality for the library: 0 failing members. Platform-neutral (no paths, OS text or certificates).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 482aeb1b d78c25d3 865591a2 e97c1b31.
- 2026-11-02: Doing -> Done. amqp:// reads a basic.return with its content and skips it, closing with 200
