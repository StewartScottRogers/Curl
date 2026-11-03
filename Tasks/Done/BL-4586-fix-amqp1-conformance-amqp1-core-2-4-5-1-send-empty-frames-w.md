---
id: BL-4586
title: Fix Amqp1 conformance: Amqp1:Core-2.4.5-1 send empty frames within the peer's idle-time-out
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4528]
touches: [Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs, Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1FrameChannel.cs, Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ProtocolHandler.cs, Curl.Extended.Protocol.Amqp1.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4586 — Fix Amqp1 conformance: Amqp1:Core-2.4.5-1 send empty frames within the peer's idle-time-out

## Goal

When the peer's open carries an idle-time-out, curl sends a frame (an empty frame when it has nothing else to send) at least every half of it, so a quiet connection is not closed under it (Part 2 section 2.4.5).

## Context

Found by BL-4528's AMQP 1.0 conformance cases against `Amqp1ReferenceContainer` (OASIS AMQP 1.0, Part 2). The client is `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs` (frames through `Amqp1FrameChannel`). Measure against the specification, not upstream curl. Case: `Amqp1ConnectionCases.IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames` (the container asks 1000 ms and holds its disposition 2500 ms; curl sends no empty frame). The timer must run on the injected `TimeProvider`.

## Acceptance criteria

- [x] `Amqp1ConnectionCases.IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames` passes and the `Amqp1:Core-2.4.5-1` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [x] A focused unit test in `Curl.Extended.Protocol.Amqp1.UnitTests` pins the empty frames on a fake clock (ADR-0482).
- [x] `Curl.Extended.Protocol.Amqp1.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`); the build is clean and the fast tests are green.

## Notes

- Touches widened to Amqp1FrameChannel.cs (writes serialised so an empty frame never interleaves with another frame) and Amqp1ProtocolHandler.cs (passes context.TimeProvider into the flow); no other task in Doing on origin/work/dark-factory names either.
- Decision (ADR-1202): when the peer's open carries an idle-time-out greater than 0, Amqp1ClientFlow runs a loop that sends an empty frame every half of it on the injected TimeProvider until the exchange ends. A fixed period was chosen over tracking the last write: the spec asks for at least one frame every half, and an empty frame costs 8 bytes.
- RunAsync awaits the exchange with SuppressThrowing, stops the loop, then rethrows. An await inside a finally left a compiler-made rethrow branch uncovered (90% branch).
- Unit tests: Amqp1ClientFlowTests pins two empty frames, at 500 ms and 1000 ms, on ManualAmqp1TimeProvider, none at 499 ms, and none for an idle-time-out of 0. Conformance Amqp1 cases pass 63/63, Core-2.4.5-1 included; its KnownFailures line is removed.
- Measure-CodeQuality (Curl.Extended.Protocol.Amqp1.UnitLibrary): 100% line, 100% branch, 0 failing members, worst CRAP 10. Full build clean; fast tests green (115 test assemblies passed). The change is platform-neutral: no paths, OS text or certificates, and the fake clock waits on events, not on scheduling order.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: c69ba9e4 1342c1dc a834f96f.
- 2026-11-02: Doing -> Done. amqp1 sends an empty frame every half of the peer's idle-time-out, timed on the injected TimeProvider
