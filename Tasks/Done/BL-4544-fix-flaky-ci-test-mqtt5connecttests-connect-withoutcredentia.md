---
id: BL-4544
title: Fix flaky CI test Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests/Mqtt5/Mqtt5ConnectTests.cs, Curl.Conformance.Extended.UnitTests/Mqtt5]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4544 — Fix flaky CI test Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst

## Goal

`Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` passes on every run.

## Context

Lane 5 found it failing about one run in three of `dotnet test Curl.Conformance.Extended.UnitTests`, at
`Curl.Conformance.Extended.UnitTests/Mqtt5/Mqtt5ConnectTests.cs:46` with "CollectionAssert.AreEqual failed.
Different number of elements.": the assertion on `broker.ReceivedPackets` (CONNECT, PUBLISH, DISCONNECT)
sometimes runs before the `Mqtt5ReferenceBroker` (from BL-4517, commit 6007992eb) has recorded the last
packet, so the broker's record races the end of the curl run. The test passes on the other runs.

## Acceptance criteria

- [x] The test waits for the broker to finish reading the connection (or the broker records every packet before the run's result is returned) rather than racing it, with no `Thread.Sleep` or fixed delay.
- [x] `dotnet test Curl.Conformance.Extended.UnitTests` passes 20 runs in a row.

## Notes

Other tests in `Curl.Conformance.Extended.UnitTests/Mqtt5` that read `broker.ReceivedPackets` likely share the race; fix them the same way.

- Cause: `ReferenceRun.EndAsync` cancels every server as soon as curl returns, and a cancelled channel read drops bytes curl already sent, so the broker could miss DISCONNECT (or PUBLISH, which curl also sends without waiting). Fix kept inside touches: `Mqtt5/ReadUntilClosedServer.cs` serves the broker without the stop signal, so it reads until curl closes the connection; `Mqtt5ConnectTests.PublishAsync` (shared by `Mqtt5DataRepresentationTests`) uses it. The SUBSCRIBE case needs no change: the broker records SUBSCRIBE before sending the SUBACK curl waits for.
- Verified: build clean; `dotnet test Curl.Conformance.Extended.UnitTests` passed 20 runs in a row (245/245 each). Test-only change in a project nothing references, so no production library to measure and no other test project affected.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: d7f69cdf 85fead5a.
- 2026-11-02: Doing -> Done. Mqtt5 tests read the broker's record after it has read curl's connection to the end, no race
