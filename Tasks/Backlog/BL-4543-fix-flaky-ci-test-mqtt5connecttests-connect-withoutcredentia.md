---
id: BL-4543
title: Fix flaky CI test Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests/Mqtt5/Mqtt5ConnectTests.cs, Curl.Conformance.Extended.UnitTests/Mqtt5]
requirement: none
created: 2026-11-02
completed:
---
# BL-4543 — Fix flaky CI test Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst

## Goal

`Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` passes on every run.

## Context

Lane 5 found it failing about one run in three of `dotnet test Curl.Conformance.Extended.UnitTests`, at
`Curl.Conformance.Extended.UnitTests/Mqtt5/Mqtt5ConnectTests.cs:46` with "CollectionAssert.AreEqual failed.
Different number of elements.": the assertion on `broker.ReceivedPackets` (CONNECT, PUBLISH, DISCONNECT)
sometimes runs before the `Mqtt5ReferenceBroker` (from BL-4517, commit 6007992eb) has recorded the last
packet, so the broker's record races the end of the curl run. The test passes on the other runs.

## Acceptance criteria

- [ ] The test waits for the broker to finish reading the connection (or the broker records every packet before the run's result is returned) rather than racing it, with no `Thread.Sleep` or fixed delay.
- [ ] `dotnet test Curl.Conformance.Extended.UnitTests` passes 20 runs in a row.

## Notes

Other tests in `Curl.Conformance.Extended.UnitTests/Mqtt5` that read `broker.ReceivedPackets` likely share the race; fix them the same way.

## Log

- 2026-11-02: Created.
