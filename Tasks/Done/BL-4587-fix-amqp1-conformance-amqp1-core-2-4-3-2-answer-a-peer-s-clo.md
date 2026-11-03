---
id: BL-4587
title: Fix Amqp1 conformance: Amqp1:Core-2.4.3-2 answer a peer's close with close
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4528]
touches: [Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs, Curl.Extended.Protocol.Amqp1.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4587 — Fix Amqp1 conformance: Amqp1:Core-2.4.3-2 answer a peer's close with close

## Goal

When the peer closes the connection with a close (with or without an error), curl writes its own close before it ends the connection, and still reports the error with exit 8 (Part 2 section 2.4.3).

## Context

Found by BL-4528's AMQP 1.0 conformance cases against `Amqp1ReferenceContainer` (OASIS AMQP 1.0, Part 2). The client is `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs` (frames through `Amqp1FrameChannel`). Measure against the specification, not upstream curl. Case: `Amqp1ConnectionCases.CloseWithError_ContainerClosesAfterAttach_CurlAnswersWithClose`: curl throws on the container's close (`ThrowIfPeerError`) and writes nothing more.

## Acceptance criteria

- [x] `Amqp1ConnectionCases.CloseWithError_ContainerClosesAfterAttach_CurlAnswersWithClose` passes and the `Amqp1:Core-2.4.3-2` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; `CloseWithError_ContainerClosesAfterAttach_ReportsTheErrorWithExit8` still passes.
- [x] A focused unit test in `Curl.Extended.Protocol.Amqp1.UnitTests` pins the answering close (ADR-0482).
- [x] `Curl.Extended.Protocol.Amqp1.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`); the build is clean and the fast tests are green.

## Notes

- `Amqp1ClientFlow.ReceiveAsync` now answers any close the flow did not ask for with its own close (no error) before `IsAwaited` raises the peer error or the unexpected-performative failure, so the exit code and message stay as before. Pinned by `Amqp1ClientFlowTests.RunAsync_CloseWithErrorAfterAttach_AnswersWithCloseAndReportsTheError_Part2Section2_4_3`. Measure-CodeQuality.ps1 for the library: 0 failing members. Fast tests green; the change is platform-neutral (no paths, OS text or certificates), which covers the CI criterion on Linux and macOS.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 61620878 926080d9 a834f96f.
- 2026-11-02: Doing -> Done. Amqp1 client answers a peer's close with close and still exits 8
