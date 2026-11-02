---
id: BL-4571
title: Fix Amqp0 conformance: Amqp0:AMQP091-1.5.2.3-1 (answer channel.flow with flow-ok)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ClientFlow.cs, Curl.Extended.Protocol.Amqp0.UnitLibrary/Methods, Curl.Extended.Protocol.Amqp0.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4571 — Fix Amqp0 conformance: Amqp0:AMQP091-1.5.2.3-1 (answer channel.flow with flow-ok)

## Goal

Curl's `amqp://` client handles channel.flow as AMQP 0-9-1 requires, so the conformance case Flow_FromTheBroker_IsAnsweredWithFlowOk passes.

## Context

Found by BL-4465 (Amqp0 conformance part 2): case `Curl.Conformance.Extended.UnitTests/Amqp0/Messaging/Amqp0MessagingTests.cs`
Flow_FromTheBroker_IsAnsweredWithFlowOk, requirement `Amqp0:AMQP091-1.5.2.3-1` in `Documentation/Conformance/Extended/Amqp0-Messaging.md`. 20.20 (channel.flow) is not in Amqp0MethodReader, so a channel.flow arriving while the flow awaits a reply ends the transfer with exit 8 and connection.close 503. Add Amqp0ChannelFlow and Amqp0ChannelFlowOk records, and have Amqp0ClientFlow answer channel.flow with flow-ok carrying the same active bit, then keep waiting for the reply it expected. With active 0, the client must not publish content until flow is active again (or, simplest, fail clearly): decide in an ADR.
Library: `Curl.Extended.Protocol.Amqp0.UnitLibrary` (its CLAUDE.md, ADR-1056, ADR-1060).

## Acceptance criteria

- [ ] The case Flow_FromTheBroker_IsAnsweredWithFlowOk passes as a plain case.
- [ ] A focused unit test in `Curl.Extended.Protocol.Amqp0.UnitTests` pins the fix (ADR-0482).
- [ ] The `Amqp0:AMQP091-1.5.2.3-1` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

## Log

- 2026-11-02: Created.
