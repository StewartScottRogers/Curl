---
id: BL-4583
title: Fix Amqp1 conformance: Amqp1:Core-2.4.5-1 send empty frames within the peer's idle-time-out
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4528]
touches: [Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs, Curl.Extended.Protocol.Amqp1.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4583 — Fix Amqp1 conformance: Amqp1:Core-2.4.5-1 send empty frames within the peer's idle-time-out

## Goal

When the peer's open carries an idle-time-out, curl sends a frame (an empty frame when it has nothing else to send) at least every half of it, so a quiet connection is not closed under it (Part 2 section 2.4.5).

## Context

Found by BL-4528's AMQP 1.0 conformance cases against `Amqp1ReferenceContainer` (OASIS AMQP 1.0, Part 2). The client is `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs` (frames through `Amqp1FrameChannel`). Measure against the specification, not upstream curl. Case: `Amqp1ConnectionCases.IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames` (the container asks 1000 ms and holds its disposition 2500 ms; curl sends no empty frame). The timer must run on the injected `TimeProvider`.

## Acceptance criteria

- [ ] `Amqp1ConnectionCases.IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames` passes and the `Amqp1:Core-2.4.5-1` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] A focused unit test in `Curl.Extended.Protocol.Amqp1.UnitTests` pins the empty frames on a fake clock (ADR-0482).
- [ ] `Curl.Extended.Protocol.Amqp1.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`); the build is clean and the fast tests are green.

## Notes

## Log

- 2026-11-02: Created.
